namespace Platform.Contracts.Events;

public record CustomerMerged(Guid CanonicalId, Guid[] MergedIds, string MergeType, DateTimeOffset MergedAt);
public record CustomerMergeRejected(Guid CandidateId, string ReviewerId, string Reason);
public record CustomerCreated(Guid CustomerId, string Email, string? Phone, string Country);
public record ConsentUpdated(Guid CustomerId, string Jurisdiction, string ConsentType, bool Granted, DateTimeOffset At);
public record DsarRequested(Guid RequestId, Guid CustomerId, string Type, string Jurisdiction);
