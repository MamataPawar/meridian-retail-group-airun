using IdentityService.Data;
using IdentityService.Domain;
using IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly IMergeEngine _mergeEngine;

    public CustomersController(IdentityDbContext db, IMergeEngine mergeEngine)
    {
        _db = db;
        _mergeEngine = mergeEngine;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? country,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = _db.Customers
            .Where(c => c.Status == "active")
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(c =>
                c.Email.Contains(q) ||
                c.FirstName.Contains(q) ||
                c.LastName.Contains(q) ||
                (c.Phone != null && c.Phone.Contains(q)));

        if (!string.IsNullOrEmpty(country))
            query = query.Where(c => c.Country == country);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.Email, c.FirstName, c.LastName, c.Phone, c.Country, c.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .Include(c => c.IdentityLinks)
            .Include(c => c.Addresses)
            .Include(c => c.Consents)
            .Include(c => c.MergeAudits)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null) return NotFound();
        return Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest req, CancellationToken ct)
    {
        var customer = new Customer
        {
            Email = req.Email,
            Phone = req.Phone,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Country = req.Country
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpGet("{id}/merge-candidates")]
    public async Task<IActionResult> GetMergeCandidates(Guid id, CancellationToken ct)
    {
        var candidates = await _mergeEngine.FindCandidatesAsync(id, ct);
        return Ok(candidates);
    }
}

[ApiController]
[Route("api/merge-queue")]
public class MergeQueueController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly IMergeEngine _mergeEngine;

    public MergeQueueController(IdentityDbContext db, IMergeEngine mergeEngine)
    {
        _db = db;
        _mergeEngine = mergeEngine;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string status = "pending",
        [FromQuery] double minScore = 0.0,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = _db.MergeCandidates
            .Include(mc => mc.CustomerA)
            .Include(mc => mc.CustomerB)
            .Where(mc => mc.Status == status && mc.ConfidenceScore >= minScore)
            .OrderByDescending(mc => mc.ConfidenceScore);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewRequest req, CancellationToken ct)
    {
        var result = await _mergeEngine.ApproveMergeAsync(id, req.ReviewerId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest req, CancellationToken ct)
    {
        var result = await _mergeEngine.RejectMergeAsync(id, req.ReviewerId, req.Reason, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("undo/{auditId}")]
    public async Task<IActionResult> Undo(Guid auditId, CancellationToken ct)
    {
        var success = await _mergeEngine.UndoMergeAsync(auditId, ct);
        return success ? Ok() : BadRequest("Cannot undo: expired or not found");
    }
}

[ApiController]
[Route("api/consents")]
public class ConsentsController : ControllerBase
{
    private readonly IdentityDbContext _db;
    public ConsentsController(IdentityDbContext db) => _db = db;

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetConsents(Guid customerId, CancellationToken ct) =>
        Ok(await _db.Consents.Where(c => c.CustomerId == customerId).ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> RecordConsent([FromBody] ConsentRequest req, CancellationToken ct)
    {
        var consent = new ConsentRecord
        {
            CustomerId = req.CustomerId,
            Jurisdiction = req.Jurisdiction,
            ConsentType = req.ConsentType,
            Granted = req.Granted,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
        _db.Consents.Add(consent);
        await _db.SaveChangesAsync(ct);
        return Ok(consent);
    }
}

[ApiController]
[Route("api/dsar")]
public class DsarController : ControllerBase
{
    private readonly IdentityDbContext _db;
    public DsarController(IdentityDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] bool overdueOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.DsarRequests.Include(d => d.Customer).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(d => d.Status == status);
        if (overdueOnly) query = query.Where(d => d.DueBy < DateTimeOffset.UtcNow && d.Status != "completed");
        return Ok(await query.OrderBy(d => d.DueBy).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DsarRequest req, CancellationToken ct)
    {
        req.DueBy = DateTimeOffset.UtcNow.AddDays(req.Jurisdiction.StartsWith("GDPR") ? 30 : 45);
        _db.DsarRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), req);
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var req = await _db.DsarRequests.FindAsync([id], ct);
        if (req is null) return NotFound();
        req.Status = "completed";
        req.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(req);
    }
}

// DTOs
public record CreateCustomerRequest(string Email, string FirstName, string LastName, string Country, string? Phone);
public record ReviewRequest(string ReviewerId);
public record RejectRequest(string ReviewerId, string Reason);
public record ConsentRequest(Guid CustomerId, string Jurisdiction, string ConsentType, bool Granted);
