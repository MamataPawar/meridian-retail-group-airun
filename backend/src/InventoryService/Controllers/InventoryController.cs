using InventoryService.Data;
using InventoryService.Services;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _db;
    private readonly IInventoryService _svc;

    public InventoryController(InventoryDbContext db, IInventoryService svc)
    {
        _db = db;
        _svc = svc;
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions(
        [FromQuery] string? storeId,
        [FromQuery] bool? lowConfidence,
        [FromQuery] bool? hiddenFromCnC,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.StockPositions.AsQueryable();
        if (!string.IsNullOrEmpty(storeId)) query = query.Where(s => s.StoreId == storeId);
        if (lowConfidence == true) query = query.Where(s => s.ConfidenceScore < 0.6);
        if (hiddenFromCnC == true) query = query.Where(s => s.HiddenFromClickAndCollect);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id, s.SkuId, s.StoreId, s.OnHand, s.Reserved, s.Buffer,
                Available = Math.Max(0, s.OnHand - s.Reserved - s.Buffer),
                s.ConfidenceScore, s.HiddenFromClickAndCollect, s.LastSapSyncAt, s.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("positions/{skuId}/{storeId}")]
    public async Task<IActionResult> GetAtp(Guid skuId, string storeId, CancellationToken ct)
    {
        var pos = await _svc.GetAtp(skuId, storeId, ct);
        return pos is null ? NotFound() : Ok(pos);
    }

    [HttpGet("drift-alerts")]
    public async Task<IActionResult> GetDriftAlerts(
        [FromQuery] string? storeId,
        [FromQuery] bool resolvedOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.DriftEvents.AsQueryable();
        if (!string.IsNullOrEmpty(storeId)) query = query.Where(d => d.StoreId == storeId);
        if (!resolvedOnly) query = query.Where(d => !d.Resolved);
        return Ok(await query.OrderByDescending(d => d.DetectedAt).Take(200).ToListAsync(ct));
    }

    [HttpPost("sap-sync")]
    public async Task<IActionResult> ProcessSapSync([FromBody] SapSyncRequest req, CancellationToken ct)
    {
        await _svc.ProcessSapSyncAsync(req.SkuId, req.StoreId, req.Quantity, ct);
        return Ok();
    }

    [HttpGet("sync-log")]
    public async Task<IActionResult> GetSyncLog(
        [FromQuery] string? storeId,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = _db.SapSyncLogs.AsQueryable();
        if (!string.IsNullOrEmpty(storeId)) query = query.Where(l => l.StoreId == storeId);
        return Ok(await query.OrderByDescending(l => l.SyncedAt).Take(take).ToListAsync(ct));
    }
}

// MassTransit consumer: respond to ReserveInventory command from order saga
public class ReserveInventoryConsumer : IConsumer<ReserveInventory>
{
    private readonly IInventoryService _svc;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<ReserveInventoryConsumer> _logger;

    public ReserveInventoryConsumer(IInventoryService svc, IPublishEndpoint bus, ILogger<ReserveInventoryConsumer> logger)
    {
        _svc = svc;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReserveInventory> context)
    {
        var cmd = context.Message;
        // For simplicity: reserve each SKU at the store embedded in the order's store context
        // In production, skuIds map to lines with quantities
        var storeId = "STORE-0001"; // Would come from order context in production
        bool allSucceeded = true;
        var failedSkus = new List<Guid>();

        foreach (var skuId in cmd.SkuIds)
        {
            var result = await _svc.ReserveAsync(cmd.OrderId, skuId, storeId, 1);
            if (!result.Success)
            {
                allSucceeded = false;
                failedSkus.Add(skuId);
                _logger.LogWarning("Reservation failed for SKU {SkuId}: {Reason}", skuId, result.Reason);
            }
        }

        if (allSucceeded)
            await _bus.Publish(new CloudEvent<InventoryReserved> { Data = new InventoryReserved(cmd.OrderId) });
        else
            await _bus.Publish(new CloudEvent<InventoryReservationFailed>
            {
                Data = new InventoryReservationFailed(cmd.OrderId, failedSkus.ToArray())
            });
    }
}

public class ReleaseInventoryConsumer : IConsumer<ReleaseInventoryReservation>
{
    private readonly IInventoryService _svc;
    public ReleaseInventoryConsumer(IInventoryService svc) => _svc = svc;

    public async Task Consume(ConsumeContext<ReleaseInventoryReservation> context) =>
        await _svc.ReleaseReservationAsync(context.Message.OrderId);
}

// DTOs
public record SapSyncRequest(Guid SkuId, string StoreId, int Quantity);
