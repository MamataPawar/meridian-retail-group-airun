using Bogus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Outbox;

namespace CatalogService;

// ── Domain ────────────────────────────────────────────────────────────────────

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<PriceBookEntry> Prices { get; set; } = new List<PriceBookEntry>();
    public ICollection<TaxRule> TaxRules { get; set; } = new List<TaxRule>();
    public ICollection<AvailabilityRule> AvailabilityRules { get; set; } = new List<AvailabilityRule>();
}

public class PriceBookEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public DateTimeOffset? SaleFrom { get; set; }
    public DateTimeOffset? SaleTo { get; set; }
}

public class TaxRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Country { get; set; } = string.Empty;
    public string TaxType { get; set; } = string.Empty;  // VAT | GST
    public decimal Rate { get; set; }
}

public class AvailabilityRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;  // web | pos | app | all
    public bool IsAvailable { get; set; } = true;
}

// ── DbContext ─────────────────────────────────────────────────────────────────

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> o) : base(o) { }
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceBookEntry> Prices => Set<PriceBookEntry>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasMany(x => x.Prices).WithOne().HasForeignKey(x => x.ProductId);
            e.HasMany(x => x.TaxRules).WithOne().HasForeignKey(x => x.ProductId);
            e.HasMany(x => x.AvailabilityRules).WithOne().HasForeignKey(x => x.ProductId);
        });
        mb.Entity<PriceBookEntry>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ProductId, x.Country }); });
        mb.Entity<TaxRule>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ProductId, x.Country }); });
        mb.Entity<AvailabilityRule>(e => e.HasKey(x => x.Id));
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Controller ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogDbContext _db;
    public CatalogController(CatalogDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category, [FromQuery] string? brand, [FromQuery] string? q,
        [FromQuery] string? country, [FromQuery] string? channel,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var query = _db.Products.Include(p => p.Prices).Include(p => p.TaxRules)
            .Where(p => p.IsActive).AsQueryable();
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.Name.Contains(q) || p.Sku.Contains(q) || p.Brand.Contains(q));
        if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrEmpty(brand)) query = query.Where(p => p.Brand == brand);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // If country is specified, project prices for that country
        if (!string.IsNullOrEmpty(country))
        {
            var projected = items.Select(p => new
            {
                p.Id, p.Sku, p.Name, p.Category, p.Brand,
                Price = p.Prices.FirstOrDefault(pr => pr.Country == country),
                Tax = p.TaxRules.FirstOrDefault(t => t.Country == country)
            });
            return Ok(new { total, page, pageSize, items = projected });
        }

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? country, CancellationToken ct)
    {
        var product = await _db.Products
            .Include(p => p.Prices)
            .Include(p => p.TaxRules)
            .Include(p => p.AvailabilityRules)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("price-books")]
    public async Task<IActionResult> GetPriceBooks([FromQuery] string country, CancellationToken ct)
    {
        var entries = await _db.Prices.Where(p => p.Country == country).Take(500).ToListAsync(ct);
        return Ok(entries);
    }
}

// ── Seed ──────────────────────────────────────────────────────────────────────

public static class SeedCatalog
{
    private static readonly string[] Countries = ["IT", "JP", "US", "FR", "DE", "GB", "ES", "AU", "CA", "NL", "BE", "AT", "PT", "PL", "SE", "NO", "DK", "FI", "CH", "SG"];
    private static readonly string[] Currencies = ["EUR", "JPY", "USD", "GBP", "EUR", "EUR", "EUR", "AUD", "CAD", "EUR", "EUR", "EUR", "EUR", "PLN", "SEK", "NOK", "DKK", "EUR", "CHF", "SGD"];
    private static readonly decimal[] VatRates = [0.22m, 0.10m, 0.00m, 0.20m, 0.19m, 0.20m, 0.21m, 0.10m, 0.05m, 0.21m, 0.21m, 0.20m, 0.23m, 0.23m, 0.25m, 0.25m, 0.25m, 0.24m, 0.077m, 0.09m];

    public static async Task SeedAsync(CatalogDbContext db, ILogger logger)
    {
        if (await db.Products.AnyAsync()) { logger.LogInformation("Catalog DB already seeded"); return; }
        logger.LogInformation("Seeding Catalog DB (20k SKUs)...");
        Randomizer.Seed = new Random(42);

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Sku, f => f.Commerce.Ean13())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Lorem.Sentence(12))
            .RuleFor(p => p.Category, f => f.Commerce.Categories(1)[0])
            .RuleFor(p => p.Brand, f => f.Company.CompanyName())
            .RuleFor(p => p.IsActive, _ => true)
            .RuleFor(p => p.CreatedAt, f => f.Date.PastOffset(3).ToUniversalTime());

        var products = productFaker.Generate(20_000);
        var faker = new Faker();

        foreach (var product in products)
        {
            var baseEur = faker.Random.Decimal(5, 800);
            for (int i = 0; i < Countries.Length; i++)
            {
                var currency = Currencies[i];
                var fx = currency switch { "JPY" => 160m, "USD" => 1.1m, "GBP" => 0.85m, "AUD" => 1.6m, "CAD" => 1.45m, "PLN" => 4.3m, "SEK" => 11.5m, "NOK" => 11.8m, "DKK" => 7.5m, "CHF" => 0.95m, "SGD" => 1.45m, _ => 1m };
                product.Prices.Add(new PriceBookEntry
                {
                    Country = Countries[i], Currency = currency,
                    Price = Math.Round(baseEur * fx, 2),
                    SalePrice = faker.Random.Bool(0.15f) ? Math.Round(baseEur * fx * 0.8m, 2) : null
                });
                product.TaxRules.Add(new TaxRule
                {
                    Country = Countries[i],
                    TaxType = Countries[i] is "JP" or "AU" or "SG" ? "GST" : "VAT",
                    Rate = VatRates[i]
                });
            }
        }

        const int batch = 100;
        for (int i = 0; i < products.Count; i += batch)
        {
            await db.Products.AddRangeAsync(products.Skip(i).Take(batch));
            await db.SaveChangesAsync();
        }
        logger.LogInformation("Catalog DB seeded: {Count} products × {Countries} price books", products.Count, Countries.Length);
    }
}
