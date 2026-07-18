namespace ToyStore.Domain.PreOrders;

public sealed class PreOrderCapacityMovement
{
    private PreOrderCapacityMovement()
    {
        Reason = null!;
        Reference = null!;
        Actor = null!;
    }

    private PreOrderCapacityMovement(
        Guid id,
        Guid capacityId,
        Guid productId,
        PreOrderCapacityMovementType type,
        int quantity,
        int availableQuantityDelta,
        int resultingRemainingQuantity,
        int resultingHeldQuantity,
        int resultingCommittedQuantity,
        int resultingRetiredQuantity,
        long resultingCapacityVersion,
        string reason,
        string reference,
        string actor,
        DateTimeOffset occurredAtUtc,
        Guid? reservationId)
    {
        Id = id;
        CapacityId = capacityId;
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        AvailableQuantityDelta = availableQuantityDelta;
        ResultingRemainingQuantity = resultingRemainingQuantity;
        ResultingHeldQuantity = resultingHeldQuantity;
        ResultingCommittedQuantity = resultingCommittedQuantity;
        ResultingRetiredQuantity = resultingRetiredQuantity;
        ResultingCapacityVersion = resultingCapacityVersion;
        Reason = reason;
        Reference = reference;
        Actor = actor;
        OccurredAtUtc = occurredAtUtc;
        ReservationId = reservationId;
    }

    public Guid Id { get; private set; }

    public Guid CapacityId { get; private set; }

    public Guid ProductId { get; private set; }

    public PreOrderCapacityMovementType Type { get; private set; }

    public int Quantity { get; private set; }

    public int AvailableQuantityDelta { get; private set; }

    public int ResultingRemainingQuantity { get; private set; }

    public int ResultingHeldQuantity { get; private set; }

    public int ResultingCommittedQuantity { get; private set; }

    public int ResultingRetiredQuantity { get; private set; }

    public long ResultingCapacityVersion { get; private set; }

    public string Reason { get; private set; }

    public string Reference { get; private set; }

    public string Actor { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? ReservationId { get; private set; }

    internal static PreOrderCapacityMovement Create(
        Guid id,
        Guid capacityId,
        Guid productId,
        PreOrderCapacityMovementType type,
        int quantity,
        int availableQuantityDelta,
        int resultingRemainingQuantity,
        int resultingHeldQuantity,
        int resultingCommittedQuantity,
        int resultingRetiredQuantity,
        long resultingCapacityVersion,
        string reason,
        string reference,
        DateTimeOffset occurredAtUtc,
        string actor,
        Guid? reservationId = null)
    {
        if (id == Guid.Empty)
        {
            throw new PreOrderCapacityRuleException(
                PreOrderCapacityRule.MovementIdentityRequired);
        }

        PreOrderCapacityEvidence.EnsureUtc(occurredAtUtc);
        return new PreOrderCapacityMovement(
            id,
            capacityId,
            productId,
            type,
            quantity,
            availableQuantityDelta,
            resultingRemainingQuantity,
            resultingHeldQuantity,
            resultingCommittedQuantity,
            resultingRetiredQuantity,
            resultingCapacityVersion,
            PreOrderCapacityEvidence.PrepareReason(reason),
            PreOrderCapacityEvidence.PrepareReference(reference),
            PreOrderCapacityEvidence.PrepareActor(actor),
            occurredAtUtc,
            reservationId);
    }
}
