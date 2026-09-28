namespace Platform.Contracts.Events;

// Cart
public record CartReserved(Guid CartId, Guid CustomerId, string Channel, string? StoreId);
public record CartMergeConflictDetected(Guid WebCartId, Guid PosCartId, Guid CustomerId, string[] ConflictingSkus);
public record CartAbandoned(Guid CartId, Guid CustomerId, DateTimeOffset AbandonedAt);

// Order saga commands
public record PlaceOrder(Guid OrderId, Guid CartId, Guid CustomerId, string Country, string Channel, string? StoreId, decimal TotalAmount, string Currency);
public record ReserveInventory(Guid OrderId, Guid[] SkuIds, string StoreId);
public record InventoryReserved(Guid OrderId);
public record InventoryReservationFailed(Guid OrderId, Guid[] UnavailableSkus);
public record ProcessPayment(Guid OrderId, Guid PaymentMethodId, decimal Amount, string Currency, bool RequiresSca);
public record PaymentCompleted(Guid OrderId, string TransactionRef);
public record PaymentFailed(Guid OrderId, string Reason);
public record OrderConfirmed(Guid OrderId, Guid CustomerId, string Channel, string Country, DateTimeOffset ConfirmedAt);
public record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset CancelledAt);
public record ReleaseInventoryReservation(Guid OrderId);
public record RefundPayment(Guid OrderId, string TransactionRef, decimal Amount);

// Click & Collect
public record ClickAndCollectReserved(Guid OrderId, string StoreId, DateTimeOffset ExpiresAt);
public record ClickAndCollectPickedUp(Guid OrderId, string StoreId, DateTimeOffset PickedUpAt, string StaffId);
public record ClickAndCollectExpired(Guid OrderId, string StoreId);

// Inventory
public record StockDriftDetected(Guid SkuId, string StoreId, int SapQuantity, int LocalQuantity, double ConfidenceScore);
public record PhantomStockFlagged(Guid SkuId, string StoreId, int ReportedStock, string DetectedBy);

// Loyalty
public record LoyaltyPointsEarned(Guid AccountId, Guid CustomerId, int Points, string Source, Guid OrderId);
public record LoyaltyAccountsMerged(Guid PrimaryAccountId, Guid[] MergedAccountIds, int TransferredPoints);

// Payment
public record ScaChallengeRequired(Guid OrderId, Guid PaymentIntentId, string ChallengeUrl);
public record ScaChallengeCompleted(Guid OrderId, Guid PaymentIntentId, bool Succeeded);
