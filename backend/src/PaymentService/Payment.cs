using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;
using Platform.Infrastructure.Outbox;

namespace PaymentService;

// ── Adapter interface ─────────────────────────────────────────────────────────

public interface IPaymentAdapter
{
    string Method { get; }  // "card" | "postepay" | "satispay" | "klarna" | "paypay"
    Task<PaymentAdapterResult> ChargeAsync(PaymentIntent intent, CancellationToken ct);
    Task<bool> RefundAsync(string transactionRef, decimal amount, CancellationToken ct);
}

public record PaymentAdapterResult(bool Success, string? TransactionRef, string? Error, bool RequiresScaRedirect, string? ScaUrl);

// ── Adapters (stubs that simulate real behaviour) ─────────────────────────────

public class CardAdapter : IPaymentAdapter
{
    public string Method => "card";
    public async Task<PaymentAdapterResult> ChargeAsync(PaymentIntent intent, CancellationToken ct)
    {
        await Task.Delay(80, ct); // simulate network
        // 18% SCA challenge rate for web channel
        var needsSca = intent.RequiresSca && intent.Amount > 30;
        if (needsSca)
            return new PaymentAdapterResult(false, null, null, true, $"https://sca.bank.example/challenge/{intent.Id}");
        var ok = Random.Shared.NextDouble() > 0.03; // 3% decline rate
        return ok
            ? new PaymentAdapterResult(true, $"TXN-CARD-{Guid.NewGuid():N}", null, false, null)
            : new PaymentAdapterResult(false, null, "Card declined", false, null);
    }
    public async Task<bool> RefundAsync(string txnRef, decimal amount, CancellationToken ct)
    { await Task.Delay(50, ct); return true; }
}

public class KlarnaAdapter : IPaymentAdapter
{
    public string Method => "klarna";
    public async Task<PaymentAdapterResult> ChargeAsync(PaymentIntent intent, CancellationToken ct)
    {
        await Task.Delay(120, ct);
        return new PaymentAdapterResult(true, $"TXN-KLARNA-{Guid.NewGuid():N}", null, false, null);
    }
    public async Task<bool> RefundAsync(string txnRef, decimal amount, CancellationToken ct)
    { await Task.Delay(60, ct); return true; }
}

public class SatispayAdapter : IPaymentAdapter
{
    public string Method => "satispay";
    public async Task<PaymentAdapterResult> ChargeAsync(PaymentIntent intent, CancellationToken ct)
    {
        await Task.Delay(90, ct);
        return new PaymentAdapterResult(true, $"TXN-SATISPAY-{Guid.NewGuid():N}", null, false, null);
    }
    public async Task<bool> RefundAsync(string txnRef, decimal amount, CancellationToken ct)
    { await Task.Delay(50, ct); return true; }
}

public class PayPayAdapter : IPaymentAdapter
{
    public string Method => "paypay";
    public async Task<PaymentAdapterResult> ChargeAsync(PaymentIntent intent, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PaymentAdapterResult(true, $"TXN-PAYPAY-{Guid.NewGuid():N}", null, false, null);
    }
    public async Task<bool> RefundAsync(string txnRef, decimal amount, CancellationToken ct)
    { await Task.Delay(50, ct); return true; }
}

// ── Domain ────────────────────────────────────────────────────────────────────

public class PaymentIntent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Method { get; set; } = "card";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool RequiresSca { get; set; }
    public string Status { get; set; } = "pending"; // pending | sca_required | completed | failed | refunded
    public string? TransactionRef { get; set; }
    public string? ScaChallengeUrl { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

// ── DbContext ─────────────────────────────────────────────────────────────────

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> o) : base(o) { }
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<PaymentIntent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.Status);
        });
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Service ───────────────────────────────────────────────────────────────────

public class PaymentOrchestrator
{
    private readonly PaymentDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly IEnumerable<IPaymentAdapter> _adapters;
    private readonly ILogger<PaymentOrchestrator> _logger;

    public PaymentOrchestrator(PaymentDbContext db, IPublishEndpoint bus,
        IEnumerable<IPaymentAdapter> adapters, ILogger<PaymentOrchestrator> logger)
    {
        _db = db; _bus = bus; _adapters = adapters; _logger = logger;
    }

    public async Task<PaymentIntent> ProcessAsync(ProcessPayment cmd, CancellationToken ct = default)
    {
        var adapter = _adapters.FirstOrDefault(a => a.Method == "card") // default to card
            ?? throw new InvalidOperationException("No card adapter registered");

        var intent = new PaymentIntent
        {
            OrderId = cmd.OrderId,
            Amount = cmd.Amount,
            Currency = cmd.Currency,
            RequiresSca = cmd.RequiresSca
        };
        _db.PaymentIntents.Add(intent);
        await _db.SaveChangesAsync(ct);

        var result = await adapter.ChargeAsync(intent, ct);

        if (result.RequiresScaRedirect)
        {
            intent.Status = "sca_required";
            intent.ScaChallengeUrl = result.ScaUrl;
            await _db.SaveChangesAsync(ct);
            await _bus.Publish(new CloudEvent<ScaChallengeRequired>
            {
                Data = new ScaChallengeRequired(cmd.OrderId, intent.Id, result.ScaUrl!)
            }, ct);
            _logger.LogInformation("SCA required for order {OrderId}", cmd.OrderId);
        }
        else if (result.Success)
        {
            intent.Status = "completed";
            intent.TransactionRef = result.TransactionRef;
            intent.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _bus.Publish(new CloudEvent<PaymentCompleted>
            {
                Data = new PaymentCompleted(cmd.OrderId, result.TransactionRef!)
            }, ct);
        }
        else
        {
            intent.Status = "failed";
            intent.FailureReason = result.Error;
            await _db.SaveChangesAsync(ct);
            await _bus.Publish(new CloudEvent<PaymentFailed>
            {
                Data = new PaymentFailed(cmd.OrderId, result.Error ?? "Unknown")
            }, ct);
        }

        return intent;
    }

    public async Task<bool> RefundAsync(Guid orderId, decimal amount, CancellationToken ct = default)
    {
        var intent = await _db.PaymentIntents
            .Where(i => i.OrderId == orderId && i.Status == "completed")
            .OrderByDescending(i => i.CompletedAt)
            .FirstOrDefaultAsync(ct);
        if (intent is null || intent.TransactionRef is null) return false;

        var adapter = _adapters.FirstOrDefault(a => a.Method == "card")!;
        var ok = await adapter.RefundAsync(intent.TransactionRef, amount, ct);
        if (ok)
        {
            intent.Status = "refunded";
            await _db.SaveChangesAsync(ct);
        }
        return ok;
    }
}

// ── Consumers ─────────────────────────────────────────────────────────────────

public class ProcessPaymentConsumer : IConsumer<ProcessPayment>
{
    private readonly PaymentOrchestrator _orchestrator;
    public ProcessPaymentConsumer(PaymentOrchestrator o) => _orchestrator = o;
    public Task Consume(ConsumeContext<ProcessPayment> ctx) =>
        _orchestrator.ProcessAsync(ctx.Message, ctx.CancellationToken);
}

public class ScaChallengeCompletedConsumer : IConsumer<CloudEvent<ScaChallengeCompleted>>
{
    private readonly PaymentDbContext _db;
    private readonly IPublishEndpoint _bus;
    public ScaChallengeCompletedConsumer(PaymentDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task Consume(ConsumeContext<CloudEvent<ScaChallengeCompleted>> ctx)
    {
        var data = ctx.Message.Data;
        var intent = await _db.PaymentIntents.FindAsync(data.PaymentIntentId);
        if (intent is null) return;

        if (data.Succeeded)
        {
            intent.Status = "completed";
            intent.TransactionRef = $"TXN-SCA-{Guid.NewGuid():N}";
            intent.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            await _bus.Publish(new CloudEvent<PaymentCompleted>
            {
                Data = new PaymentCompleted(data.OrderId, intent.TransactionRef)
            });
        }
        else
        {
            intent.Status = "failed";
            await _db.SaveChangesAsync();
            await _bus.Publish(new CloudEvent<PaymentFailed>
            {
                Data = new PaymentFailed(data.OrderId, "SCA challenge failed")
            });
        }
    }
}

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _db;
    private readonly PaymentOrchestrator _orchestrator;

    public PaymentsController(PaymentDbContext db, PaymentOrchestrator o) { _db = db; _orchestrator = o; }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct) =>
        Ok(await _db.PaymentIntents.Where(p => p.OrderId == orderId).ToListAsync(ct));

    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ProcessPaymentRequest req, CancellationToken ct)
    {
        var intent = await _orchestrator.ProcessAsync(
            new ProcessPayment(req.OrderId, req.PaymentMethodId, req.Amount, req.Currency, req.RequiresSca), ct);
        return Ok(intent);
    }

    [HttpPost("{orderId}/refund")]
    public async Task<IActionResult> Refund(Guid orderId, [FromBody] RefundRequest req, CancellationToken ct)
    {
        var ok = await _orchestrator.RefundAsync(orderId, req.Amount, ct);
        return ok ? Ok() : BadRequest("Refund failed or no completed payment found");
    }

    [HttpPost("sca/{intentId}/complete")]
    public async Task<IActionResult> CompleteScaChallenge(Guid intentId, [FromBody] ScaCompleteRequest req, CancellationToken ct)
    {
        var intent = await _db.PaymentIntents.FindAsync([intentId], ct);
        if (intent is null) return NotFound();
        // Simulate SCA webhook from bank
        return Ok(new { intentId, orderId = intent.OrderId, succeeded = req.Succeeded });
    }
}

public record ProcessPaymentRequest(Guid OrderId, Guid PaymentMethodId, decimal Amount, string Currency, bool RequiresSca);
public record RefundRequest(decimal Amount);
public record ScaCompleteRequest(bool Succeeded);
