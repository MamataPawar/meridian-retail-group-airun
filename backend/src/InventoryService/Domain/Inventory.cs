namespace InventoryService.Domain;

public class StockPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SkuId { get; set; }
    public string StoreId { get; set; } = string.Empty;

    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public int Buffer { get; set; } = 2;           // safety buffer
    public int Available => Math.Max(0, OnHand - Reserved - Buffer);

    public double ConfidenceScore { get; set; } = 1.0; // 0.0-1.0; below 0.6 = low confidence
    public bool HiddenFromClickAndCollect { get; set; }

    public DateTimeOffset LastSapSyncAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<SapSyncLog> SyncLogs { get; set; } = new List<SapSyncLog>();
}

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SkuId { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "active";   // active | released | fulfilled
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);
    public DateTimeOffset? ReleasedAt { get; set; }

    public StockPosition? StockPosition { get; set; }
}

public class SapSyncLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SkuId { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public int SapQuantity { get; set; }
    public int LocalQuantityAtSync { get; set; }
    public int Drift => Math.Abs(SapQuantity - LocalQuantityAtSync);
    public bool DriftAlerted { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class DriftEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SkuId { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public int SapQuantity { get; set; }
    public int LocalQuantity { get; set; }
    public double ConfidenceScore { get; set; }
    public string Severity { get; set; } = "warning"; // warning | critical
    public bool Resolved { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
}
