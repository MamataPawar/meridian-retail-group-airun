namespace Platform.Infrastructure.Security;

/// <summary>
/// Marks a property as PII — drives encryption at rest and log redaction.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PiiAttribute : Attribute
{
    public PiiEncryptionLevel Level { get; }
    public PiiAttribute(PiiEncryptionLevel level = PiiEncryptionLevel.Encrypted) => Level = level;
}

public enum PiiEncryptionLevel { Encrypted, Hashed, Redacted }
