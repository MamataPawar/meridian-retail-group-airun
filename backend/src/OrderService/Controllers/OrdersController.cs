using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Domain;
using Platform.Contracts.Events;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly IPublishEndpoint _bus;

    public OrdersController(OrderDbContext db, IPublishEndpoint bus)
    {
        _db = db;
        _bus = bus;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] string? channel,
        [FromQuery] string? storeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = _db.Orders.AsQueryable();
        if (customerId.HasValue) query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrEmpty(channel)) query = query.Where(o => o.Channel == channel);
        if (!string.IsNullOrEmpty(storeId)) query = query.Where(o => o.StoreId == storeId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.Events)
            .Include(o => o.Fulfilments)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>
    /// Saga Inspector: shows current saga state + transition history for an order.
    /// Drives the visual state machine in the Ops Console.
    /// </summary>
    [HttpGet("{id}/saga")]
    public async Task<IActionResult> GetSagaState(Guid id, CancellationToken ct)
    {
        var saga = await _db.PlaceOrderSagas.FirstOrDefaultAsync(s => s.OrderId == id, ct);
        var events = await _db.OrderEvents.Where(e => e.OrderId == id).OrderBy(e => e.OccurredAt).ToListAsync(ct);

        var states = new[]
        {
            "Initial", "WaitingForInventory", "WaitingForPayment",
            "WaitingForScaChallenge", "Confirmed", "CompensatingInventory", "Cancelled"
        };

        return Ok(new
        {
            OrderId = id,
            CurrentState = saga?.CurrentState ?? "Unknown",
            States = states,
            Transitions = events.Select(e => new { e.Type, e.Details, e.OccurredAt }),
            FailureReason = saga?.FailureReason,
            CompensationPath = saga?.CurrentState == "Cancelled"
                ? new[] { "ReleaseInventoryReservation", "RefundPayment" }
                : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest req, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            CustomerId = req.CustomerId,
            CartId = req.CartId,
            Channel = req.Channel,
            StoreId = req.StoreId,
            Country = req.Country,
            TotalAmount = req.TotalAmount,
            Currency = req.Currency ?? "EUR",
            IsClickAndCollect = req.IsClickAndCollect,
            PickupExpiresAt = req.IsClickAndCollect ? DateTimeOffset.UtcNow.AddHours(48) : null,
            PickupStatus = req.IsClickAndCollect ? "reserved" : null,
            Status = "pending",
            SagaState = "Initial"
        };

        foreach (var line in req.Lines)
        {
            order.Lines.Add(new OrderLine
            {
                SkuId = line.SkuId,
                SkuName = line.SkuName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Kick off saga
        await _bus.Publish(new PlaceOrder(
            orderId, req.CartId ?? Guid.NewGuid(), req.CustomerId,
            req.Country, req.Channel, req.StoreId, req.TotalAmount, req.Currency ?? "EUR"), ct);

        return CreatedAtAction(nameof(Get), new { id = orderId }, new { orderId });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest req, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync([id], ct);
        if (order is null) return NotFound();
        if (order.Status is "confirmed" or "shipped" or "delivered" or "cancelled")
            return BadRequest($"Cannot cancel order in state {order.Status}");

        await _bus.Publish(new OrderCancelled(id, req.Reason, DateTimeOffset.UtcNow), ct);
        return Ok();
    }

    // Click & Collect endpoints
    [HttpGet("click-and-collect")]
    public async Task<IActionResult> GetClickAndCollectQueue(
        [FromQuery] string storeId,
        CancellationToken ct = default)
    {
        var orders = await _db.Orders
            .Where(o => o.IsClickAndCollect && o.StoreId == storeId
                     && o.PickupStatus == "reserved")
            .OrderBy(o => o.PickupExpiresAt)
            .ToListAsync(ct);
        return Ok(orders);
    }

    [HttpPost("{id}/pickup")]
    public async Task<IActionResult> MarkPickedUp(Guid id, [FromBody] PickupRequest req, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync([id], ct);
        if (order is null) return NotFound();

        order.PickupStatus = "collected";
        order.Status = "delivered";
        order.UpdatedAt = DateTimeOffset.UtcNow;

        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = id,
            Type = "ClickAndCollectPickedUp",
            Details = $"Staff: {req.StaffId}, Store: {req.StoreId}"
        });

        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new CloudEvent<ClickAndCollectPickedUp>
        {
            Data = new ClickAndCollectPickedUp(id, req.StoreId, DateTimeOffset.UtcNow, req.StaffId),
            StoreId = req.StoreId
        }, ct);

        return Ok();
    }
}

// Consumers that update Order status based on saga events
public class OrderConfirmedConsumer : IConsumer<CloudEvent<OrderConfirmed>>
{
    private readonly OrderDbContext _db;
    public OrderConfirmedConsumer(OrderDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<CloudEvent<OrderConfirmed>> context)
    {
        var order = await _db.Orders.FindAsync(context.Message.Data.OrderId);
        if (order is null) return;
        order.Status = "confirmed";
        order.SagaState = "Confirmed";
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _db.OrderEvents.Add(new OrderEvent { OrderId = order.Id, Type = "Confirmed" });
        await _db.SaveChangesAsync();
    }
}

public class OrderCancelledConsumer : IConsumer<OrderCancelled>
{
    private readonly OrderDbContext _db;
    public OrderCancelledConsumer(OrderDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var order = await _db.Orders.FindAsync(context.Message.OrderId);
        if (order is null) return;
        order.Status = "cancelled";
        order.FailureReason = context.Message.Reason;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}

// DTOs
public record PlaceOrderRequest(
    Guid CustomerId, Guid? CartId, string Channel, string? StoreId,
    string Country, decimal TotalAmount, string? Currency,
    bool IsClickAndCollect, List<OrderLineRequest> Lines);
public record OrderLineRequest(Guid SkuId, string SkuName, int Quantity, decimal UnitPrice, decimal VatRate);
public record CancelRequest(string Reason);
public record PickupRequest(string StaffId, string StoreId);
