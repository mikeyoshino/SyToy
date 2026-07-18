namespace ToyStore.Domain.PreOrders;

public sealed class PreOrderCapacityReservation
{
    private PreOrderCapacityReservation()
    {
        CustomerId = null!;
        ReserveReason = null!;
        ReserveReference = null!;
        ReservedBy = null!;
    }

    internal PreOrderCapacityReservation(
        Guid id,
        Guid capacityId,
        Guid productId,
        Guid checkoutAttemptId,
        string customerId,
        int quantity,
        DateTimeOffset reservedAtUtc,
        DateTimeOffset expiresAtUtc,
        Guid reserveMovementId,
        string reserveReason,
        string reserveReference,
        string reservedBy)
    {
        Id = id;
        CapacityId = capacityId;
        ProductId = productId;
        CheckoutAttemptId = checkoutAttemptId;
        CustomerId = customerId;
        Quantity = quantity;
        ReservedAtUtc = reservedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ReserveMovementId = reserveMovementId;
        ReserveReason = reserveReason;
        ReserveReference = reserveReference;
        ReservedBy = reservedBy;
        Status = PreOrderCapacityReservationStatus.Active;
    }

    public Guid Id { get; private set; }
    public Guid CapacityId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid CheckoutAttemptId { get; private set; }
    public string CustomerId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset ReservedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid ReserveMovementId { get; private set; }
    public string ReserveReason { get; private set; }
    public string ReserveReference { get; private set; }
    public string ReservedBy { get; private set; }
    public PreOrderCapacityReservationStatus Status { get; private set; }
    public DateTimeOffset? TransitionAtUtc { get; private set; }
    public string? TransitionActor { get; private set; }
    public string? TransitionReason { get; private set; }
    public string? TransitionReference { get; private set; }
    public Guid? TransitionMovementId { get; private set; }
    public Guid? ConsumedMovementId { get; private set; }
    public PreOrderCancellationKind? CancellationKind { get; private set; }
    public PreOrderDepositDisposition? DepositDisposition { get; private set; }

    public bool IsEffectiveActiveAt(DateTimeOffset nowUtc)
    {
        PreOrderCapacityEvidence.EnsureUtc(nowUtc);
        return Status == PreOrderCapacityReservationStatus.Active
            && nowUtc < ExpiresAtUtc;
    }

    internal void ApplyTransition(
        PreOrderCapacityReservationStatus status,
        DateTimeOffset transitionAtUtc,
        string actor,
        string reason,
        string reference,
        Guid movementId,
        Guid? consumedMovementId = null,
        PreOrderCancellationKind? cancellationKind = null,
        PreOrderDepositDisposition? depositDisposition = null)
    {
        Status = status;
        TransitionAtUtc = transitionAtUtc;
        TransitionActor = actor;
        TransitionReason = reason;
        TransitionReference = reference;
        TransitionMovementId = movementId;
        ConsumedMovementId = consumedMovementId ?? ConsumedMovementId;
        CancellationKind = cancellationKind;
        DepositDisposition = depositDisposition;
    }
}
