namespace ToyStore.Domain.Inventory;

public sealed class StockMovement
{
    private StockMovement()
    {
        Reason = null!;
        Reference = null!;
        Actor = null!;
    }

    private StockMovement(
        Guid id,
        Guid inventoryItemId,
        Guid productId,
        StockMovementType type,
        int quantityDelta,
        int resultingOnHandQuantity,
        long resultingInventoryVersion,
        string reason,
        string reference,
        string actor,
        DateTimeOffset occurredAtUtc,
        Guid? reservationId)
    {
        Id = id;
        InventoryItemId = inventoryItemId;
        ProductId = productId;
        Type = type;
        QuantityDelta = quantityDelta;
        ResultingOnHandQuantity = resultingOnHandQuantity;
        ResultingInventoryVersion = resultingInventoryVersion;
        Reason = reason;
        Reference = reference;
        Actor = actor;
        OccurredAtUtc = occurredAtUtc;
        ReservationId = reservationId;
    }

    public Guid Id { get; private set; }

    public Guid InventoryItemId { get; private set; }

    public Guid ProductId { get; private set; }

    public StockMovementType Type { get; private set; }

    public int QuantityDelta { get; private set; }

    public int ResultingOnHandQuantity { get; private set; }

    public long ResultingInventoryVersion { get; private set; }

    public string Reason { get; private set; }

    public string Reference { get; private set; }

    public string Actor { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? ReservationId { get; private set; }

    internal static StockMovement Create(
        Guid id,
        Guid inventoryItemId,
        Guid productId,
        StockMovementType type,
        int quantityDelta,
        int resultingOnHandQuantity,
        long resultingInventoryVersion,
        string reason,
        string reference,
        DateTimeOffset occurredAtUtc,
        string actor,
        Guid? reservationId = null)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryRuleException(InventoryRule.MovementIdentityRequired);
        }

        InventoryEvidence.EnsureUtc(occurredAtUtc);
        var preparedReason = InventoryEvidence.PrepareReason(reason);
        var preparedReference = InventoryEvidence.PrepareReference(reference);
        var preparedActor = InventoryEvidence.PrepareActor(actor);
        return new StockMovement(
            id,
            inventoryItemId,
            productId,
            type,
            quantityDelta,
            resultingOnHandQuantity,
            resultingInventoryVersion,
            preparedReason,
            preparedReference,
            preparedActor,
            occurredAtUtc,
            reservationId);
    }
}
