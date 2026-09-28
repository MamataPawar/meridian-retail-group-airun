using Bogus;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;
using Platform.Infrastructure.Outbox;

namespace LoyaltyService;

// ── Domain ───────────────────────────────────────────────────────────────────

public class LoyaltyAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public int Points { get; set; }
    public string Tier { get; set; } = "bronze"; // bronze | silver | gold | platinum
    public bool IsActive { get; set; } = true;
    public Guid? MergedIntoId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<LedgerEntry> Ledger { get; set; } = new List<LedgerEntry>();
}

public class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public int Delta { get; set; }       // positive = earn, negative = redeem
    public string Type { get; set; } = string.Empty; // earn | redeem | adjust | transfer
    public string Source { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public string? StaffId { get; set; }  // for manual adjustments
    public string? AuditNote { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

// ── DbContext ─────────────────────────────────────────────────────────────────

public class LoyaltyDbContext : DbContext
{
    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> o) : base(o) { }
    public DbSet<LoyaltyAccount> Accounts => Set<LoyaltyAccount>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<LoyaltyAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId);
            e.HasMany(x => x.Ledger).WithOne().HasForeignKey(x => x.AccountId);
        });
        mb.Entity<LedgerEntry>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.AccountId); });
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Service ───────────────────────────────────────────────────────────────────

public class LoyaltySvc
{
    private readonly LoyaltyDbContext _db;
    private readonly IPublishEndpoint _bus;

    private static string ComputeTier(int points) => points switch
    {
        >= 10000 => "platinum",
        >= 5000 => "gold",
        >= 1000 => "silver",
        _ => "bronze"
    };

    public LoyaltySvc(LoyaltyDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<LedgerEntry> EarnPointsAsync(Guid customerId, int points, string source, Guid? orderId, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsActive, ct)
            ?? throw new InvalidOperationException("No active loyalty account");

        account.Points += points;
        account.Tier = ComputeTier(account.Points);

        var entry = new LedgerEntry { AccountId = account.Id, Delta = points, Type = "earn", Source = source, OrderId = orderId };
        _db.Ledger.Add(entry);
        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new CloudEvent<LoyaltyPointsEarned>
        {
            Data = new LoyaltyPointsEarned(account.Id, customerId, points, source, orderId ?? Guid.Empty)
        }, ct);
        return entry;
    }

    public async Task<bool> MergeAccountsAsync(Guid primaryId, Guid[] subordinateIds, CancellationToken ct)
    {
        var primary = await _db.Accounts.FindAsync([primaryId], ct);
        if (primary is null) return false;

        int transferredPoints = 0;
        foreach (var subId in subordinateIds)
        {
            var sub = await _db.Accounts.FindAsync([subId], ct);
            if (sub is null) continue;
            transferredPoints += sub.Points;
            primary.Points += sub.Points;
            sub.Points = 0;
            sub.IsActive = false;
            sub.MergedIntoId = primaryId;

            _db.Ledger.Add(new LedgerEntry
            {
                AccountId = primaryId, Delta = sub.Points, Type = "transfer",
                Source = $"merge from {subId}", AuditNote = "Account merge"
            });
        }

        primary.Tier = ComputeTier(primary.Points);
        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new CloudEvent<LoyaltyAccountsMerged>
        {
            Data = new LoyaltyAccountsMerged(primaryId, subordinateIds, transferredPoints)
        }, ct);
        return true;
    }
}

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/loyalty")]
public class LoyaltyController : ControllerBase
{
    private readonly LoyaltyDbContext _db;
    private readonly LoyaltySvc _svc;

    public LoyaltyController(LoyaltyDbContext db, LoyaltySvc svc) { _db = db; _svc = svc; }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> Get(Guid customerId, CancellationToken ct)
    {
        var account = await _db.Accounts.Include(a => a.Ledger).FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsActive, ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost("{customerId}/earn")]
    public async Task<IActionResult> Earn(Guid customerId, [FromBody] EarnRequest req, CancellationToken ct)
    {
        var entry = await _svc.EarnPointsAsync(customerId, req.Points, req.Source, req.OrderId, ct);
        return Ok(entry);
    }

    [HttpPost("{customerId}/adjust")]
    public async Task<IActionResult> Adjust(Guid customerId, [FromBody] AdjustRequest req, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsActive, ct);
        if (account is null) return NotFound();
        account.Points += req.Delta;
        account.Tier = req.Delta switch { >= 10000 => "platinum", >= 5000 => "gold", >= 1000 => "silver", _ => account.Tier };
        _db.Ledger.Add(new LedgerEntry { AccountId = account.Id, Delta = req.Delta, Type = "adjust", Source = "manual", StaffId = req.StaffId, AuditNote = req.Note });
        await _db.SaveChangesAsync(ct);
        return Ok(account);
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeRequest req, CancellationToken ct)
    {
        var ok = await _svc.MergeAccountsAsync(req.PrimaryAccountId, req.SubordinateIds, ct);
        return ok ? Ok() : BadRequest("Primary account not found");
    }

    [HttpGet("fragmented")]
    public async Task<IActionResult> GetFragmented(CancellationToken ct)
    {
        // Customers with more than one active account (the 210-customer problem)
        var fragmented = await _db.Accounts
            .Where(a => a.IsActive)
            .GroupBy(a => a.CustomerId)
            .Where(g => g.Count() > 1)
            .Select(g => new { CustomerId = g.Key, AccountCount = g.Count(), TotalPoints = g.Sum(a => a.Points) })
            .ToListAsync(ct);
        return Ok(fragmented);
    }
}

// ── Seed ──────────────────────────────────────────────────────────────────────

public static class SeedLoyalty
{
    public static async Task SeedAsync(LoyaltyDbContext db, ILogger logger)
    {
        if (await db.Accounts.AnyAsync()) { logger.LogInformation("Loyalty DB already seeded"); return; }
        logger.LogInformation("Seeding Loyalty DB...");
        Randomizer.Seed = new Random(42);
        var faker = new Faker();

        var customerIds = Enumerable.Range(1, 50_000).Select(_ => Guid.NewGuid()).ToArray();
        var accounts = customerIds.Select(cid =>
        {
            var pts = faker.Random.Int(0, 15000);
            return new LoyaltyAccount
            {
                CustomerId = cid,
                Points = pts,
                Tier = pts switch { >= 10000 => "platinum", >= 5000 => "gold", >= 1000 => "silver", _ => "bronze" }
            };
        }).ToList();

        // 210 fragmented: create a second account for the same customer
        for (int i = 0; i < 210; i++)
        {
            accounts.Add(new LoyaltyAccount
            {
                CustomerId = customerIds[i],
                Points = faker.Random.Int(100, 2000),
                Tier = "bronze"
            });
        }

        const int batch = 1000;
        for (int i = 0; i < accounts.Count; i += batch)
        {
            await db.Accounts.AddRangeAsync(accounts.Skip(i).Take(batch));
            await db.SaveChangesAsync();
        }
        logger.LogInformation("Loyalty DB seeded: {Count} accounts (210 fragmented)", accounts.Count);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record EarnRequest(int Points, string Source, Guid? OrderId);
public record AdjustRequest(int Delta, string StaffId, string? Note);
public record MergeRequest(Guid PrimaryAccountId, Guid[] SubordinateIds);
