using Bogus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Outbox;

namespace StoreService;

// ── Domain ────────────────────────────────────────────────────────────────────

public class Store
{
    public string Id { get; set; } = string.Empty;  // e.g. "STORE-0001"
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool ClickAndCollectEnabled { get; set; } = true;
    public bool IsChronicallyOffline { get; set; } = false;
    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}

public class Terminal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreId { get; set; } = string.Empty;
    public string TerminalCode { get; set; } = string.Empty;
    public string Status { get; set; } = "online";   // online | offline | maintenance
    public int OfflineQueueDepth { get; set; }        // pending transactions while offline
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;
    public string? CurrentStaffId { get; set; }
    public Store Store { get; set; } = null!;
}

public class StaffSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreId { get; set; } = string.Empty;
    public Guid TerminalId { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
}

// ── DbContext ─────────────────────────────────────────────────────────────────

public class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> o) : base(o) { }
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<StaffSession> StaffSessions => Set<StaffSession>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Store>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Country);
            e.HasMany(x => x.Terminals).WithOne(t => t.Store).HasForeignKey(t => t.StoreId);
        });
        mb.Entity<Terminal>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StoreId);
            e.HasIndex(x => x.Status);
        });
        mb.Entity<StaffSession>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.StoreId); });
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/stores")]
public class StoresController : ControllerBase
{
    private readonly StoreDbContext _db;
    public StoresController(StoreDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? country, [FromQuery] bool? offline, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var q = _db.Stores.Include(s => s.Terminals).AsQueryable();
        if (!string.IsNullOrEmpty(country)) q = q.Where(s => s.Country == country);
        if (offline == true) q = q.Where(s => s.IsChronicallyOffline);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(s => s.Country).ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var store = await _db.Stores.Include(s => s.Terminals).FirstOrDefaultAsync(s => s.Id == id, ct);
        return store is null ? NotFound() : Ok(store);
    }

    [HttpGet("{id}/terminals")]
    public async Task<IActionResult> GetTerminals(string id, CancellationToken ct) =>
        Ok(await _db.Terminals.Where(t => t.StoreId == id).ToListAsync(ct));

    [HttpPost("{storeId}/terminals/{terminalId}/heartbeat")]
    public async Task<IActionResult> Heartbeat(string storeId, Guid terminalId, [FromBody] HeartbeatRequest req, CancellationToken ct)
    {
        var terminal = await _db.Terminals.FindAsync([terminalId], ct);
        if (terminal is null) return NotFound();
        terminal.LastHeartbeat = DateTimeOffset.UtcNow;
        terminal.Status = req.Online ? "online" : "offline";
        terminal.OfflineQueueDepth = req.QueueDepth;
        await _db.SaveChangesAsync(ct);
        return Ok(terminal);
    }

    [HttpGet("health-summary")]
    public async Task<IActionResult> HealthSummary(CancellationToken ct)
    {
        var stores = await _db.Stores.Include(s => s.Terminals).ToListAsync(ct);
        var staleThreshold = DateTimeOffset.UtcNow.AddMinutes(-5);
        return Ok(new
        {
            Total = stores.Count,
            Online = stores.Count(s => s.Terminals.Any(t => t.LastHeartbeat >= staleThreshold)),
            ChronicallyOffline = stores.Count(s => s.IsChronicallyOffline),
            TotalTerminals = stores.Sum(s => s.Terminals.Count),
            OfflineTerminals = stores.Sum(s => s.Terminals.Count(t => t.Status == "offline")),
            TotalOfflineQueueDepth = stores.Sum(s => s.Terminals.Sum(t => t.OfflineQueueDepth))
        });
    }
}

public record HeartbeatRequest(bool Online, int QueueDepth);

// ── Seed ──────────────────────────────────────────────────────────────────────

public static class SeedStores
{
    private static readonly (string Country, string[] Cities)[] StoreLocations =
    [
        ("IT", ["Rome", "Milan", "Naples", "Turin", "Florence", "Venice", "Bologna", "Genoa"]),
        ("JP", ["Tokyo", "Osaka", "Kyoto", "Yokohama", "Nagoya", "Sapporo", "Fukuoka", "Kobe"]),
        ("US", ["New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio", "San Diego"]),
        ("FR", ["Paris", "Lyon", "Marseille", "Toulouse", "Nice", "Nantes", "Bordeaux", "Lille"]),
        ("DE", ["Berlin", "Munich", "Frankfurt", "Hamburg", "Cologne", "Stuttgart", "Düsseldorf", "Leipzig"]),
        ("GB", ["London", "Birmingham", "Manchester", "Glasgow", "Leeds", "Sheffield", "Edinburgh", "Bristol"])
    ];

    public static async Task SeedAsync(StoreDbContext db, ILogger logger)
    {
        if (await db.Stores.AnyAsync()) { logger.LogInformation("Store DB already seeded"); return; }
        logger.LogInformation("Seeding Store DB (200 stores, 600 terminals)...");
        Randomizer.Seed = new Random(42);
        var faker = new Faker();

        var stores = new List<Store>();
        int storeNum = 1;
        // 6 chronically-offline stores (one per country)
        var offlineCountries = StoreLocations.Select(l => l.Country).ToHashSet();

        foreach (var (country, cities) in StoreLocations)
        {
            int storesForCountry = 200 / StoreLocations.Length;
            bool addedOffline = false;

            for (int i = 0; i < storesForCountry; i++)
            {
                var city = cities[i % cities.Length];
                var isOffline = !addedOffline && faker.Random.Bool(0.05f);
                if (isOffline) addedOffline = true;

                var store = new Store
                {
                    Id = $"STORE-{storeNum:D4}",
                    Name = $"{faker.Company.CompanyName()} {city}",
                    Country = country,
                    City = city,
                    Address = faker.Address.StreetAddress(),
                    IsChronicallyOffline = isOffline,
                    ClickAndCollectEnabled = !isOffline
                };

                // 3 terminals per store = 600 total
                for (int t = 0; t < 3; t++)
                {
                    var terminalOnline = !isOffline || faker.Random.Bool(0.3f);
                    store.Terminals.Add(new Terminal
                    {
                        StoreId = store.Id,
                        TerminalCode = $"POS-{storeNum:D4}-{t + 1}",
                        Status = terminalOnline ? "online" : "offline",
                        OfflineQueueDepth = terminalOnline ? 0 : faker.Random.Int(0, 120),
                        LastHeartbeat = terminalOnline
                            ? DateTimeOffset.UtcNow.AddSeconds(-faker.Random.Int(10, 240))
                            : DateTimeOffset.UtcNow.AddHours(-faker.Random.Int(2, 72))
                    });
                }

                stores.Add(store);
                storeNum++;
            }
        }

        await db.Stores.AddRangeAsync(stores);
        await db.SaveChangesAsync();
        logger.LogInformation("Store DB seeded: {Stores} stores, {Terminals} terminals ({Offline} chronically offline)",
            stores.Count, stores.Sum(s => s.Terminals.Count), stores.Count(s => s.IsChronicallyOffline));
    }
}
