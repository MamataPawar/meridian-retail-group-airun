using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;
using Platform.Infrastructure.Outbox;

namespace CartService;

// ── Domain ──────────────────────────────────────────────────────────────────

public class Cart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Channel { get; set; } = string.Empty;    // web | pos | app
    public string? StoreId { get; set; }
    public string Status { get; set; } = "active";         // active | abandoned | checked_out | merged
    public string? MergedFromChannel { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public ICollection<CartLine> Lines { get; set; } = new List<CartLine>();
}

public class CartLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CartId { get; set; }
    public Guid SkuId { get; set; }
    public string SkuName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? PromoCode { get; set; }
}

public class CartMergeConflict
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebCartId { get; set; }
    public Guid PosCartId { get; set; }
    public Guid CustomerId { get; set; }
    public string ConflictingSkus { get; set; } = "[]"; // JSON array
    public string Resolution { get; set; } = "pending"; // pending | web_wins | pos_wins | merged
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

// ── DbContext ────────────────────────────────────────────────────────────────

public class CartDbContext : DbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> o) : base(o) { }
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartLine> CartLines => Set<CartLine>();
    public DbSet<CartMergeConflict> MergeConflicts => Set<CartMergeConflict>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Cart>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.CartId);
        });
        mb.Entity<CartLine>(e => e.HasKey(x => x.Id));
        mb.Entity<CartMergeConflict>(e => e.HasKey(x => x.Id));
        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

// ── Service ──────────────────────────────────────────────────────────────────

public class CartService
{
    private readonly CartDbContext _db;
    private readonly IPublishEndpoint _bus;

    public CartService(CartDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Cart> GetOrCreateAsync(Guid customerId, string channel, string? storeId, CancellationToken ct = default)
    {
        var cart = await _db.Carts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Channel == channel && c.Status == "active", ct);

        if (cart is null)
        {
            cart = new Cart { CustomerId = customerId, Channel = channel, StoreId = storeId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        return cart;
    }

    /// <summary>
    /// Resume a web cart on POS. Detects conflicts when same SKU exists in both carts
    /// with different quantities (the 4% conflict rate scenario).
    /// </summary>
    public async Task<CartMergeResult> ResumeOnPosAsync(
        Guid customerId, string storeId, CancellationToken ct = default)
    {
        var webCart = await _db.Carts.Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Channel == "web" && c.Status == "active", ct);

        var posCart = await _db.Carts.Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Channel == "pos" && c.Status == "active", ct);

        if (webCart is null) return new CartMergeResult(posCart ?? new Cart(), [], false);

        if (posCart is null)
        {
            // No POS cart — just adopt the web cart
            var adopted = new Cart
            {
                CustomerId = customerId,
                Channel = "pos",
                StoreId = storeId,
                MergedFromChannel = "web",
                Lines = webCart.Lines.Select(l => new CartLine
                {
                    SkuId = l.SkuId, SkuName = l.SkuName, Quantity = l.Quantity, UnitPrice = l.UnitPrice
                }).ToList()
            };
            webCart.Status = "merged";
            _db.Carts.Add(adopted);
            await _db.SaveChangesAsync(ct);
            return new CartMergeResult(adopted, [], false);
        }

        // Find conflicts: same SKU, different qty
        var conflicts = webCart.Lines
            .Join(posCart.Lines, w => w.SkuId, p => p.SkuId,
                (w, p) => new { w.SkuId, WebQty = w.Quantity, PosQty = p.Quantity })
            .Where(x => x.WebQty != x.PosQty)
            .Select(x => x.SkuId.ToString())
            .ToArray();

        if (conflicts.Length > 0)
        {
            var conflict = new CartMergeConflict
            {
                WebCartId = webCart.Id,
                PosCartId = posCart.Id,
                CustomerId = customerId,
                ConflictingSkus = System.Text.Json.JsonSerializer.Serialize(conflicts)
            };
            _db.MergeConflicts.Add(conflict);
            await _db.SaveChangesAsync(ct);

            await _bus.Publish(new CloudEvent<CartMergeConflictDetected>
            {
                Data = new CartMergeConflictDetected(webCart.Id, posCart.Id, customerId, conflicts),
                Channel = "pos", StoreId = storeId
            }, ct);

            return new CartMergeResult(posCart, conflicts, true);
        }

        // No conflicts — merge: add web-only lines to POS cart
        var posSkus = posCart.Lines.Select(l => l.SkuId).ToHashSet();
        foreach (var line in webCart.Lines.Where(l => !posSkus.Contains(l.SkuId)))
            posCart.Lines.Add(new CartLine { SkuId = line.SkuId, SkuName = line.SkuName, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

        webCart.Status = "merged";
        posCart.MergedFromChannel = "web";
        posCart.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new CartMergeResult(posCart, [], false);
    }
}

public record CartMergeResult(Cart Cart, string[] ConflictingSkus, bool HasConflict);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Route("api/carts")]
public class CartsController : ControllerBase
{
    private readonly CartDbContext _db;
    private readonly CartService _svc;

    public CartsController(CartDbContext db, CartService svc) { _db = db; _svc = svc; }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? channel,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var q = _db.Carts.Include(c => c.Lines).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status == status);
        if (!string.IsNullOrEmpty(channel)) q = q.Where(c => c.Channel == channel);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.UpdatedAt).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var cart = await _db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id, ct);
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetForCustomer(Guid customerId, [FromQuery] string? channel, CancellationToken ct)
    {
        var q = _db.Carts.Include(c => c.Lines).Where(c => c.CustomerId == customerId && c.Status == "active");
        if (!string.IsNullOrEmpty(channel)) q = q.Where(c => c.Channel == channel);
        return Ok(await q.ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCartRequest req, CancellationToken ct)
    {
        var cart = await _svc.GetOrCreateAsync(req.CustomerId, req.Channel, req.StoreId, ct);
        return Ok(cart);
    }

    [HttpPost("{id}/lines")]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddLineRequest req, CancellationToken ct)
    {
        var cart = await _db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cart is null) return NotFound();

        var existing = cart.Lines.FirstOrDefault(l => l.SkuId == req.SkuId);
        if (existing is not null) existing.Quantity += req.Quantity;
        else cart.Lines.Add(new CartLine { CartId = id, SkuId = req.SkuId, SkuName = req.SkuName, Quantity = req.Quantity, UnitPrice = req.UnitPrice });

        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(cart);
    }

    [HttpPost("resume-on-pos")]
    public async Task<IActionResult> ResumeOnPos([FromBody] ResumeRequest req, CancellationToken ct)
    {
        var result = await _svc.ResumeOnPosAsync(req.CustomerId, req.StoreId, ct);
        return Ok(result);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts([FromQuery] string? resolution = "pending", CancellationToken ct = default)
    {
        var q = _db.MergeConflicts.AsQueryable();
        if (!string.IsNullOrEmpty(resolution)) q = q.Where(c => c.Resolution == resolution);
        return Ok(await q.OrderByDescending(c => c.DetectedAt).Take(100).ToListAsync(ct));
    }

    [HttpPost("conflicts/{id}/resolve")]
    public async Task<IActionResult> ResolveConflict(Guid id, [FromBody] ResolveConflictRequest req, CancellationToken ct)
    {
        var conflict = await _db.MergeConflicts.FindAsync([id], ct);
        if (conflict is null) return NotFound();
        conflict.Resolution = req.Resolution;
        conflict.ResolvedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(conflict);
    }
}

public record CreateCartRequest(Guid CustomerId, string Channel, string? StoreId);
public record AddLineRequest(Guid SkuId, string SkuName, int Quantity, decimal UnitPrice);
public record ResumeRequest(Guid CustomerId, string StoreId);
public record ResolveConflictRequest(string Resolution);
