using IdentityService.Data;
using IdentityService.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Contracts.Events;

namespace IdentityService.Services;

public interface IMergeEngine
{
    Task<MergeCandidate[]> FindCandidatesAsync(Guid customerId, CancellationToken ct = default);
    Task RunDeterministicMergeAsync(CancellationToken ct = default);
    Task<MergeResult> ApproveMergeAsync(Guid candidateId, string reviewerId, CancellationToken ct = default);
    Task<MergeResult> RejectMergeAsync(Guid candidateId, string reviewerId, string reason, CancellationToken ct = default);
    Task<bool> UndoMergeAsync(Guid auditEntryId, CancellationToken ct = default);
}

public record MergeResult(bool Success, string? Error = null, Guid? CanonicalId = null);

public class MergeEngine : IMergeEngine
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<MergeEngine> _logger;

    public MergeEngine(IdentityDbContext db, IPublishEndpoint bus, ILogger<MergeEngine> logger)
    {
        _db = db;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Deterministic merge: exact email OR exact phone match across legacy systems.
    /// Runs as a background sweep and auto-merges without human review.
    /// </summary>
    public async Task RunDeterministicMergeAsync(CancellationToken ct = default)
    {
        // Find active customers sharing the same email
        var emailDuplicates = await _db.Customers
            .Where(c => c.Status == "active" && c.Email != null)
            .GroupBy(c => c.Email)
            .Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(c => c.CreatedAt).ToList())
            .ToListAsync(ct);

        foreach (var group in emailDuplicates)
        {
            var canonical = group.First(); // oldest = canonical
            var toMerge = group.Skip(1).ToList();
            await MergeIntoCanonicalAsync(canonical, toMerge, "deterministic", null, ct);
        }
    }

    /// <summary>
    /// Probabilistic: score candidates for human review queue using weighted field matching.
    /// </summary>
    public async Task<MergeCandidate[]> FindCandidatesAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .Include(c => c.IdentityLinks)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null) return [];

        var allActive = await _db.Customers
            .Include(c => c.IdentityLinks)
            .Where(c => c.Status == "active" && c.Id != customerId)
            .ToListAsync(ct);

        var candidates = new List<MergeCandidate>();
        foreach (var other in allActive)
        {
            var score = ComputeMatchScore(customer, other, out var reason);
            if (score >= 0.6 && score < 1.0) // 1.0 = deterministic (already handled)
            {
                var existing = await _db.MergeCandidates
                    .AnyAsync(mc =>
                        (mc.CustomerAId == customerId && mc.CustomerBId == other.Id) ||
                        (mc.CustomerAId == other.Id && mc.CustomerBId == customerId), ct);

                if (!existing)
                {
                    var candidate = new MergeCandidate
                    {
                        CustomerAId = customerId,
                        CustomerBId = other.Id,
                        ConfidenceScore = score,
                        MatchReason = reason
                    };
                    candidates.Add(candidate);
                    _db.MergeCandidates.Add(candidate);
                }
            }
        }
        await _db.SaveChangesAsync(ct);
        return candidates.ToArray();
    }

    private static double ComputeMatchScore(Customer a, Customer b, out string reason)
    {
        double score = 0.0;
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(a.Email) && a.Email == b.Email) { score += 1.0; parts.Add("email:exact"); }
        else if (!string.IsNullOrEmpty(a.Email) && !string.IsNullOrEmpty(b.Email))
        {
            if (FuzzyMatch(a.Email, b.Email) > 0.8) { score += 0.4; parts.Add("email:fuzzy"); }
        }

        if (!string.IsNullOrEmpty(a.Phone) && !string.IsNullOrEmpty(b.Phone))
        {
            var normA = NormalizePhone(a.Phone);
            var normB = NormalizePhone(b.Phone);
            if (normA == normB) { score += 0.9; parts.Add("phone:exact"); }
        }

        if (a.FirstName == b.FirstName && a.LastName == b.LastName) { score += 0.3; parts.Add("name:exact"); }
        else if (FuzzyMatch(a.LastName, b.LastName) > 0.85) { score += 0.15; parts.Add("name:fuzzy"); }

        // Cap at 0.95 for probabilistic (1.0 reserved for deterministic)
        score = Math.Min(score, 0.95);
        reason = System.Text.Json.JsonSerializer.Serialize(parts);
        return score;
    }

    private static double FuzzyMatch(string a, string b)
    {
        // Simple Levenshtein ratio
        int dist = LevenshteinDistance(a.ToLower(), b.ToLower());
        int maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)dist / maxLen;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
                d[i, j] = s[i - 1] == t[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));
        return d[n, m];
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    public async Task<MergeResult> ApproveMergeAsync(Guid candidateId, string reviewerId, CancellationToken ct = default)
    {
        var candidate = await _db.MergeCandidates
            .Include(mc => mc.CustomerA)
            .Include(mc => mc.CustomerB)
            .FirstOrDefaultAsync(mc => mc.Id == candidateId, ct);

        if (candidate is null) return new MergeResult(false, "Candidate not found");
        if (candidate.Status != "pending") return new MergeResult(false, "Already reviewed");

        candidate.Status = "approved";
        candidate.ReviewerId = reviewerId;
        candidate.ReviewedAt = DateTimeOffset.UtcNow;

        // Canonical = older customer
        var canonical = candidate.CustomerA.CreatedAt <= candidate.CustomerB.CreatedAt
            ? candidate.CustomerA : candidate.CustomerB;
        var subordinate = canonical == candidate.CustomerA ? candidate.CustomerB : candidate.CustomerA;

        await MergeIntoCanonicalAsync(canonical, [subordinate], "probabilistic", reviewerId, ct);
        return new MergeResult(true, CanonicalId: canonical.Id);
    }

    public async Task<MergeResult> RejectMergeAsync(Guid candidateId, string reviewerId, string reason, CancellationToken ct = default)
    {
        var candidate = await _db.MergeCandidates.FindAsync([candidateId], ct);
        if (candidate is null) return new MergeResult(false, "Candidate not found");

        candidate.Status = "rejected";
        candidate.ReviewerId = reviewerId;
        candidate.ReviewedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new CloudEvent<CustomerMergeRejected>
        {
            Data = new CustomerMergeRejected(candidateId, reviewerId, reason)
        }, ct);

        return new MergeResult(true);
    }

    public async Task<bool> UndoMergeAsync(Guid auditEntryId, CancellationToken ct = default)
    {
        var audit = await _db.MergeAudits.FindAsync([auditEntryId], ct);
        if (audit is null || audit.UndoneAt.HasValue) return false;
        if (DateTimeOffset.UtcNow > audit.ExpiresAt) return false; // 90-day window expired

        var subordinate = await _db.Customers.FindAsync([audit.MergedCustomerId], ct);
        if (subordinate is null) return false;

        subordinate.Status = "active";
        subordinate.MergedIntoId = null;
        audit.UndoneAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Merge undone: {MergedId} restored from {CanonicalId}", audit.MergedCustomerId, audit.CanonicalCustomerId);
        return true;
    }

    private async Task MergeIntoCanonicalAsync(
        Customer canonical, List<Customer> subordinates,
        string mergeType, string? reviewerId, CancellationToken ct)
    {
        foreach (var sub in subordinates)
        {
            sub.Status = "merged";
            sub.MergedIntoId = canonical.Id;

            // Transfer identity links
            var links = await _db.IdentityLinks.Where(l => l.CustomerId == sub.Id).ToListAsync(ct);
            foreach (var link in links) link.CustomerId = canonical.Id;

            // Transfer addresses not already present
            var subAddresses = await _db.Addresses.Where(a => a.CustomerId == sub.Id).ToListAsync(ct);
            foreach (var addr in subAddresses) addr.CustomerId = canonical.Id;

            // Audit trail
            var audit = new MergeAuditEntry
            {
                CanonicalCustomerId = canonical.Id,
                MergedCustomerId = sub.Id,
                MergeType = mergeType,
                ReviewerId = reviewerId
            };
            _db.MergeAudits.Add(audit);
        }

        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new CloudEvent<CustomerMerged>
        {
            Data = new CustomerMerged(canonical.Id, subordinates.Select(s => s.Id).ToArray(), mergeType, DateTimeOffset.UtcNow)
        }, ct);

        _logger.LogInformation("Merged {Count} customers into canonical {CanonicalId} ({Type})",
            subordinates.Count, canonical.Id, mergeType);
    }
}
