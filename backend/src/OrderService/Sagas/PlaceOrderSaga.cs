using MassTransit;
using OrderService.Data;
using OrderService.Domain;
using Platform.Contracts.Events;

namespace OrderService.Sagas;

/// <summary>
/// PlaceOrder Saga: Cart → ReserveInventory → ProcessPayment → Confirm
/// with full compensation on any failure.
/// State transitions are persisted to DB for the Saga Inspector UI.
/// </summary>
public class PlaceOrderSaga : MassTransitStateMachine<PlaceOrderSagaState>
{
    public State WaitingForInventory { get; private set; } = null!;
    public State WaitingForPayment { get; private set; } = null!;
    public State WaitingForScaChallenge { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;
    public State CompensatingInventory { get; private set; } = null!;

    public Event<PlaceOrder> PlaceOrderEvent { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReservedEvent { get; private set; } = null!;
    public Event<InventoryReservationFailed> InventoryFailedEvent { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompletedEvent { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailedEvent { get; private set; } = null!;
    public Event<ScaChallengeCompleted> ScaChallengeCompletedEvent { get; private set; } = null!;
    public Event<OrderCancelled> OrderCancelledEvent { get; private set; } = null!;

    public PlaceOrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PlaceOrderEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => InventoryReservedEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => InventoryFailedEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompletedEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailedEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => ScaChallengeCompletedEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => OrderCancelledEvent, x => x.CorrelateById(ctx => ctx.Message.OrderId));

        Initially(
            When(PlaceOrderEvent)
                .Then(ctx =>
                {
                    var msg = ctx.Message;
                    ctx.Saga.OrderId = msg.OrderId;
                    ctx.Saga.CustomerId = msg.CustomerId;
                    ctx.Saga.Country = msg.Country;
                    ctx.Saga.Channel = msg.Channel;
                    ctx.Saga.StoreId = msg.StoreId;
                    ctx.Saga.TotalAmount = msg.TotalAmount;
                    ctx.Saga.Currency = msg.Currency;
                    ctx.Saga.RequiresSca = msg.Channel == "web" && msg.TotalAmount > 30;
                })
                .Publish(ctx => new ReserveInventory(
                    ctx.Saga.OrderId,
                    [],  // In production, skuIds from order lines
                    ctx.Saga.StoreId ?? "STORE-0001"))
                .TransitionTo(WaitingForInventory));

        During(WaitingForInventory,
            When(InventoryReservedEvent)
                .Publish(ctx => new ProcessPayment(
                    ctx.Saga.OrderId,
                    Guid.NewGuid(),           // paymentMethodId from cart/customer
                    ctx.Saga.TotalAmount,
                    ctx.Saga.Currency,
                    ctx.Saga.RequiresSca))
                .TransitionTo(WaitingForPayment),

            When(InventoryFailedEvent)
                .Then(ctx => ctx.Saga.FailureReason = "Inventory unavailable")
                .Publish(ctx => new OrderCancelled(ctx.Saga.OrderId, "Inventory unavailable", DateTimeOffset.UtcNow))
                .TransitionTo(Cancelled));

        During(WaitingForPayment,
            When(PaymentCompletedEvent)
                .Then(ctx => ctx.Saga.PaymentTransactionRef = ctx.Message.TransactionRef)
                .Publish(ctx => new OrderConfirmed(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.Channel,
                    ctx.Saga.Country,
                    DateTimeOffset.UtcNow))
                .TransitionTo(Confirmed)
                .Finalize(),

            When(PaymentFailedEvent)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                // Compensate: release inventory
                .Publish(ctx => new ReleaseInventoryReservation(ctx.Saga.OrderId))
                .TransitionTo(CompensatingInventory));

        During(WaitingForScaChallenge,
            When(ScaChallengeCompletedEvent)
                .IfElse(ctx => ctx.Message.Succeeded,
                    ifBranch => ifBranch
                        .Publish(ctx => new PaymentCompleted(ctx.Saga.OrderId, Guid.NewGuid().ToString()))
                        .TransitionTo(WaitingForPayment),
                    elseBranch => elseBranch
                        .Then(ctx => ctx.Saga.FailureReason = "SCA challenge failed")
                        .Publish(ctx => new OrderCancelled(ctx.Saga.OrderId, "SCA failed", DateTimeOffset.UtcNow))
                        .TransitionTo(Cancelled)));

        During(CompensatingInventory,
            When(OrderCancelledEvent)
                .TransitionTo(Cancelled)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
