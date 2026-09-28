namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid? CartId { get; set; }

    public string Status { get; set; } = "pending";
    // pending → inventory_reserved → payment_processing → confirmed → shipped → delivered
    // Any step → cancelled (with compensation)

    public string Channel { get; set; } = string.Empty;   // web | pos | app
    public string? StoreId { get; set; }
    public string Country { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";

    public string? PaymentTransactionRef { get; set; }
    public string? FailureReason { get; set; }
    public string? SagaState { get; set; }  // current saga step for inspector

    public bool IsClickAndCollect { get; set; }
    public DateTimeOffset? PickupExpiresAt { get; set; }
    public string? PickupStatus { get; set; } // reserved | picked | collected | expired | cancelled

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public ICollection<OrderEvent> Events { get; set; } = new List<OrderEvent>();
    public ICollection<Fulfilment> Fulfilments { get; set; } = new List<Fulfilment>();
}

public class OrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid SkuId { get; set; }
    public string SkuName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class OrderEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Fulfilment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Type { get; set; } = string.Empty;   // "ship" | "click_and_collect"
    public string Status { get; set; } = "pending";
    public string? TrackingRef { get; set; }
    public string? StoreId { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
}

// Saga state — stored in DB via MassTransit EF Core saga persistence
public class PlaceOrderSagaState : MassTransit.SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? StoreId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? PaymentTransactionRef { get; set; }
    public string? FailureReason { get; set; }
    public bool RequiresSca { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
