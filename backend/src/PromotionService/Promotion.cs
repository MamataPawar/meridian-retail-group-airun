using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Outbox;

namespace PromotionService;

// ── Domain ────────────────────────────────────────────────────────────────────

public class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "percentage"; // percentage | fixed | bogo | free_shipping
    public decimal DiscountValue { get; set; }        // % or fixed amount
    public string? Country { get; set; }              // null = all countries
    public string? Channel { get; set; }              // null = all channels
    public string? Category { get; set; }             // null = all categories
    public decimal? MinOrderValue { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public bool StackingAllowed { get; set; } = false;
    public int StackingPriority { get; set; } = 0;   // higher = applied first
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class StackingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string[] AllowedTypes { get; set; } = [];   // which promotion types can stack
    public int MaxStack { get; set; } = 1;
    public string? Country { get; set; }
}

public class PromotionConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromotionAId { get; set; }
    public Guid PromotionBId { get; set; }
    public string ConflictType { get; set; } = string.Empty; // "region_overlap" | "stacking_violation" | "date_overlap"
    public bool Resolved { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ── DbContext ─────────────────────────────────────────────────────────────────

public class PromotionDbContext : DbContext
{
    public PromotionDbContext(DbContextOptions<PromotionDbContext> o) : base(o) { }
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<StackingRule> StackingRules => Set<StackingRule>();
    public DbSet<PromotionConflict> Conflicts => Set<PromotionConflict>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Promotion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Country, x.IsActive });
        });
        mb.Entity<StackingRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AllowedTypes).HasColumnType("jsonb");
        });
        mb.Entity<PromotionConflict>(e => e.HasKey(x => x.Id));
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Rule Engine ───────────────────────────────────────────────────────────────

public interface IPromotionRule
{
    string Name { get; }
    bool Applies(Promotion promotion, PromotionContext context);
    decimal ComputeDiscount(Promotion promotion, decimal orderTotal);
}

public record PromotionContext(string Country, string Channel, string? Category, decimal OrderTotal, DateTimeOffset At);

public class PercentageRule : IPromotionRule
{
    public string Name => "percentage";
    public bool Applies(Promotion p, PromotionContext ctx) =>
        p.Type == "percentage" &&
        (p.Country == null || p.Country == ctx.Country) &&
        (p.Channel == null || p.Channel == ctx.Channel) &&
        (p.MinOrderValue == null || ctx.OrderTotal >= p.MinOrderValue) &&
        ctx.At >= p.StartsAt && ctx.At <= p.EndsAt && p.IsActive;

    public decimal ComputeDiscount(Promotion p, decimal total) =>
        Math.Round(total * (p.DiscountValue / 100), 2);
}

public class FixedDiscountRule : IPromotionRule
{
    public string Name => "fixed";
    public bool Applies(Promotion p, PromotionContext ctx) =>
        p.Type == "fixed" &&
        (p.Country == null || p.Country == ctx.Country) &&
        (p.MinOrderValue == null || ctx.OrderTotal >= p.MinOrderValue) &&
        ctx.At >= p.StartsAt && ctx.At <= p.EndsAt && p.IsActive;

    public decimal ComputeDiscount(Promotion p, decimal total) =>
        Math.Min(p.DiscountValue, total);
}

public class PromotionEngine
{
    private readonly PromotionDbContext _db;
    private readonly IEnumerable<IPromotionRule> _rules;

    public PromotionEngine(PromotionDbContext db, IEnumerable<IPromotionRule> rules)
    { _db = db; _rules = rules; }

    public async Task<ApplyResult> ApplyAsync(string? code, PromotionContext ctx, CancellationToken ct = default)
    {
        var query = _db.Promotions.Where(p => p.IsActive && p.StartsAt <= ctx.At && p.EndsAt >= ctx.At);
        if (!string.IsNullOrEmpty(code)) query = query.Where(p => p.Code == code);

        var promos = await query
            .Where(p => p.Country == null || p.Country == ctx.Country)
            .OrderByDescending(p => p.StackingPriority)
            .ToListAsync(ct);

        var applied = new List<AppliedPromo>();
        decimal remaining = ctx.OrderTotal;

        foreach (var promo in promos)
        {
            var rule = _rules.FirstOrDefault(r => r.Name == promo.Type);
            if (rule is null || !rule.Applies(promo, ctx)) continue;

            // Stacking check
            if (!promo.StackingAllowed && applied.Count > 0) continue;

            var discount = rule.ComputeDiscount(promo, remaining);
            if (discount <= 0) continue;

            applied.Add(new AppliedPromo(promo.Id, promo.Name, promo.Code, discount));
            remaining -= discount;

            // Increment usage (fire-and-forget)
            promo.UsageCount++;
        }

        await _db.SaveChangesAsync(ct);
        return new ApplyResult(ctx.OrderTotal, remaining, applied);
    }

    /// <summary>Detect conflicts: overlapping promos in the same region that cannot both be active.</summary>
    public async Task<List<PromotionConflict>> DetectConflictsAsync(CancellationToken ct = default)
    {
        var active = await _db.Promotions.Where(p => p.IsActive).ToListAsync(ct);
        var conflicts = new List<PromotionConflict>();

        for (int i = 0; i < active.Count; i++)
        {
            for (int j = i + 1; j < active.Count; j++)
            {
                var a = active[i];
                var b = active[j];

                // Region overlap: same country, same channel, overlapping dates, neither allows stacking
                bool regionOverlap =
                    (a.Country == b.Country || a.Country == null || b.Country == null) &&
                    (a.Channel == b.Channel || a.Channel == null || b.Channel == null) &&
                    a.StartsAt < b.EndsAt && a.EndsAt > b.StartsAt &&
                    !a.StackingAllowed && !b.StackingAllowed;

                if (regionOverlap)
                {
                    var existing = await _db.Conflicts.AnyAsync(c =>
                        (c.PromotionAId == a.Id && c.PromotionBId == b.Id) ||
                        (c.PromotionAId == b.Id && c.PromotionBId == a.Id), ct);
                    if (!existing)
                    {
                        var conflict = new PromotionConflict
                        {
                            PromotionAId = a.Id,
                            PromotionBId = b.Id,
                            ConflictType = "region_overlap"
                        };
                        _db.Conflicts.Add(conflict);
                        conflicts.Add(conflict);
                    }
                }
            }
        }
        await _db.SaveChangesAsync(ct);
        return conflicts;
    }
}

public record AppliedPromo(Guid PromotionId, string Name, string Code, decimal Discount);
public record ApplyResult(decimal OriginalTotal, decimal FinalTotal, List<AppliedPromo> Applied);

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/promotions")]
public class PromotionsController : ControllerBase
{
    private readonly PromotionDbContext _db;
    private readonly PromotionEngine _engine;

    public PromotionsController(PromotionDbContext db, PromotionEngine engine) { _db = db; _engine = engine; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? country, [FromQuery] bool? active, CancellationToken ct)
    {
        var q = _db.Promotions.AsQueryable();
        if (!string.IsNullOrEmpty(country)) q = q.Where(p => p.Country == null || p.Country == country);
        if (active.HasValue) q = q.Where(p => p.IsActive == active);
        return Ok(await q.OrderByDescending(p => p.StartsAt).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Promotion promo, CancellationToken ct)
    {
        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync(ct);
        // Auto-detect conflicts after creating new promo
        await _engine.DetectConflictsAsync(ct);
        return CreatedAtAction(nameof(List), promo);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyPromoRequest req, CancellationToken ct)
    {
        var ctx = new PromotionContext(req.Country, req.Channel, req.Category, req.OrderTotal, DateTimeOffset.UtcNow);
        var result = await _engine.ApplyAsync(req.Code, ctx, ct);
        return Ok(result);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] bool resolved = false, CancellationToken ct = default)
    {
        var conflicts = await _db.Conflicts.Where(c => c.Resolved == resolved).ToListAsync(ct);
        return Ok(conflicts);
    }

    [HttpPost("detect-conflicts")]
    public async Task<IActionResult> DetectConflicts(CancellationToken ct)
    {
        var found = await _engine.DetectConflictsAsync(ct);
        return Ok(new { detected = found.Count, conflicts = found });
    }
}

public record ApplyPromoRequest(string? Code, string Country, string Channel, string? Category, decimal OrderTotal);
