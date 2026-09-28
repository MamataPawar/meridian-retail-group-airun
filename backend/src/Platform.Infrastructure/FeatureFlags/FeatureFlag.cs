namespace Platform.Infrastructure.FeatureFlags;

public class FeatureFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Country { get; set; }      // null = all countries
    public string? Channel { get; set; }      // null = all channels
    public string? Capability { get; set; }   // e.g. "SCA", "ClickAndCollect"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, string? country = null, string? channel = null);
}
