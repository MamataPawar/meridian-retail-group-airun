using Platform.Infrastructure.Security;

namespace IdentityService.Domain;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Pii] public string Email { get; set; } = string.Empty;
    [Pii] public string? Phone { get; set; }
    [Pii] public string FirstName { get; set; } = string.Empty;
    [Pii] public string LastName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
    public string Status { get; set; } = "active"; // active | merged | deleted
    public Guid? MergedIntoId { get; set; }        // if merged, canonical target

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<IdentityLink> IdentityLinks { get; set; } = new List<IdentityLink>();
    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    public ICollection<ConsentRecord> Consents { get; set; } = new List<ConsentRecord>();
    public ICollection<MergeAuditEntry> MergeAudits { get; set; } = new List<MergeAuditEntry>();
}

public class IdentityLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string System { get; set; } = string.Empty;   // "Shopify-IT" | ".NET-JP" | "POS-US"
    public string ExternalId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CustomerAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    [Pii] public string Line1 { get; set; } = string.Empty;
    [Pii] public string? Line2 { get; set; }
    [Pii] public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class ConsentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Jurisdiction { get; set; } = string.Empty; // "GDPR-IT" | "PDPA-JP" | "CCPA-US"
    public string ConsentType { get; set; } = string.Empty;  // "marketing" | "analytics" | "data_sharing"
    public bool Granted { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class MergeAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CanonicalCustomerId { get; set; }
    public Guid MergedCustomerId { get; set; }
    public string MergeType { get; set; } = string.Empty; // "deterministic" | "probabilistic"
    public string? ReviewerId { get; set; }
    public DateTimeOffset MergedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UndoneAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(90);
}

public class MergeCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerAId { get; set; }
    public Customer CustomerA { get; set; } = null!;
    public Guid CustomerBId { get; set; }
    public Customer CustomerB { get; set; } = null!;
    public double ConfidenceScore { get; set; }
    public string MatchReason { get; set; } = string.Empty; // JSON: which fields matched
    public string Status { get; set; } = "pending";          // pending | approved | rejected
    public string? ReviewerId { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
}

public class DsarRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Type { get; set; } = string.Empty;         // "export" | "delete"
    public string Jurisdiction { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";          // pending | processing | completed | overdue
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueBy { get; set; }                 // 30 days for GDPR
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ExportPath { get; set; }
}
