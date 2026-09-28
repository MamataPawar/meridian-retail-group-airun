using InventoryService.Data;
using InventoryService.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<StockPosition?> GetAtp(Guid skuId, string storeId, CancellationToken ct = default);
    Task<ReservationResult> ReserveAsync(Guid orderId, Guid skuId, string storeId, int qty, CancellationToken ct = default);
    Task ReleaseReservationAsync(Guid orderId, CancellationToken ct = default);
    Task ProcessSapSyncAsync(Guid skuId, string storeId, int sapQty, CancellationToken ct = default);
    Task ExpireReservationsAsync(CancellationToken ct = default);
}

public record ReservationResult(bool Success, string? Reason = null, int Available = 0);

public class InventoryServiceImpl : IInventoryService
{
    private readonly InventoryDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<InventoryServiceImpl> _logger;

    // Phantom-stock: 12 stores with artificially injected bad data
    private static readonly HashSet<string> PhantomStockStores =
        Enumerable.Range(1, 12).Select(i => $"STORE-{i:D4}").ToHashSet();

    public InventoryServiceImpl(InventoryDbContext db, IPublishEndpoint bus, ILogger<InventoryServiceImpl> logger)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
    }

    public async Task<StockPosition?> GetAtp(Guid skuId, string storeId, CancellationToken ct = default)
    {
        var pos = await _db.StockPositions
            .FirstOrDefaultAsync(s => s.SkuId == skuId && s.StoreId == storeId, ct);

        if (pos is not null)
        {
            // Recompute reservations (release expired ones in memory)
            var activeReservations = await _db.Reservations
                .Where(r => r.SkuId == skuId && r.StoreId == storeId
                         && r.Status == "active" && r.ExpiresAt > DateTimeOffset.UtcNow)
                .SumAsync(r => r.Quantity, ct);

            pos.Reserved = activeReservations;
        }
        return pos;
    }

    public async Task<ReservationResult> ReserveAsync(
        Guid orderId, Guid skuId, string storeId, int qty, CancellationToken ct = default)
    {
        // Pessimistic lock on stock position
        var pos = await _db.StockPositions
            .FromSqlRaw("SELECT * FROM \"StockPositions\" WHERE \"SkuId\" = {0} AND \"StoreId\" = {1} FOR UPDATE", skuId, storeId)
            .FirstOrDefaultAsync(ct);

        if (pos is null)
            return new ReservationResult(false, "SKU not available at store");

        if (pos.ConfidenceScore < 0.6)
            return new ReservationResult(false, $"Low confidence stock ({pos.ConfidenceScore:P0}) — manual check required", pos.Available);

        if (pos.Available < qty)
            return new ReservationResult(false, $"Insufficient stock: {pos.Available} available, {qty} requested", pos.Available);

        var reservation = new Reservation
        {
            OrderId = orderId,
            SkuId = skuId,
            StoreId = storeId,
            Quantity = qty,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        pos.Reserved += qty;
        pos.UpdatedAt = DateTimeOffset.UtcNow;
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Reserved {Qty} x {SkuId} at {StoreId} for order {OrderId}",
            qty, skuId, storeId, orderId);

        return new ReservationResult(true, Available: pos.Available);
    }

    public async Task ReleaseReservationAsync(Guid orderId, CancellationToken ct = default)
    {
        var reservations = await _db.Reservations
            .Where(r => r.OrderId == orderId && r.Status == "active")
            .ToListAsync(ct);

        foreach (var res in reservations)
        {
            res.Status = "released";
            res.ReleasedAt = DateTimeOffset.UtcNow;

            var pos = await _db.StockPositions
                .FirstOrDefaultAsync(s => s.SkuId == res.SkuId && s.StoreId == res.StoreId, ct);
            if (pos is not null)
            {
                pos.Reserved = Math.Max(0, pos.Reserved - res.Quantity);
                pos.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ProcessSapSyncAsync(Guid skuId, string storeId, int sapQty, CancellationToken ct = default)
    {
        var pos = await _db.StockPositions
            .FirstOrDefaultAsync(s => s.SkuId == skuId && s.StoreId == storeId, ct);

        if (pos is null)
        {
            pos = new StockPosition { SkuId = skuId, StoreId = storeId, OnHand = sapQty };
            _db.StockPositions.Add(pos);
        }

        // Inject phantom stock for designated stores (7.1% problem)
        var isPhantomStore = PhantomStockStores.Contains(storeId);
        var localQty = pos.OnHand;

        var drift = Math.Abs(sapQty - localQty);
        var driftRatio = localQty == 0 ? 0 : (double)drift / localQty;

        // Confidence scoring: high drift → low confidence
        pos.ConfidenceScore = isPhantomStore
            ? Math.Max(0.2, pos.ConfidenceScore - 0.15)  // phantom stores degrade confidence
            : Math.Max(0.4, 1.0 - driftRatio);

        pos.OnHand = sapQty;
        pos.LastSapSyncAt = DateTimeOffset.UtcNow;
        pos.UpdatedAt = DateTimeOffset.UtcNow;
        pos.HiddenFromClickAndCollect = pos.ConfidenceScore < 0.6;

        var syncLog = new SapSyncLog
        {
            SkuId = skuId,
            StoreId = storeId,
            SapQuantity = sapQty,
            LocalQuantityAtSync = localQty
        };
        _db.SapSyncLogs.Add(syncLog);

        if (drift > 5 || driftRatio > 0.2)
        {
            var driftEvent = new DriftEvent
            {
                SkuId = skuId,
                StoreId = storeId,
                SapQuantity = sapQty,
                LocalQuantity = localQty,
                ConfidenceScore = pos.ConfidenceScore,
                Severity = driftRatio > 0.5 ? "critical" : "warning"
            };
            _db.DriftEvents.Add(driftEvent);

            await _bus.Publish(new CloudEvent<StockDriftDetected>
            {
                Data = new StockDriftDetected(skuId, storeId, sapQty, localQty, pos.ConfidenceScore),
                StoreId = storeId
            }, ct);

            if (isPhantomStore)
            {
                await _bus.Publish(new CloudEvent<PhantomStockFlagged>
                {
                    Data = new PhantomStockFlagged(skuId, storeId, sapQty, "SAPSync"),
                    StoreId = storeId
                }, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ExpireReservationsAsync(CancellationToken ct = default)
    {
        var expired = await _db.Reservations
            .Where(r => r.Status == "active" && r.ExpiresAt <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var res in expired)
        {
            res.Status = "released";
            res.ReleasedAt = DateTimeOffset.UtcNow;

            var pos = await _db.StockPositions
                .FirstOrDefaultAsync(s => s.SkuId == res.SkuId && s.StoreId == res.StoreId, ct);
            if (pos is not null)
                pos.Reserved = Math.Max(0, pos.Reserved - res.Quantity);
        }

        if (expired.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Expired {Count} reservations", expired.Count);
        }
    }
}
