namespace Platform.Contracts.Events;

public record CloudEvent<T>
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Type { get; init; } = typeof(T).Name;
    public string Source { get; init; } = "retail-platform";
    public DateTimeOffset Time { get; init; } = DateTimeOffset.UtcNow;
    public string? Country { get; init; }
    public string? Channel { get; init; }
    public string? StoreId { get; init; }
    public string TraceId { get; init; } = Guid.NewGuid().ToString();
    public T Data { get; init; } = default!;
}
