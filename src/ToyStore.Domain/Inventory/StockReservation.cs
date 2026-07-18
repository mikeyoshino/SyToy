namespace ToyStore.Domain.Inventory;

public sealed class StockReservation
{
    private StockReservation()
    {
        ReserveReason = null!;
        ReserveReference = null!;
        ReservedBy = null!;
    }

    internal StockReservation(
        Guid id,
        Guid inventoryItemId,
        Guid productId,
        Guid checkoutAttemptId,
        int quantity,
        DateTimeOffset reservedAtUtc,
        DateTimeOffset expiresAtUtc,
        string reserveReason,
        string reserveReference,
        string reservedBy)
    {
        Id = id;
        InventoryItemId = inventoryItemId;
        ProductId = productId;
        CheckoutAttemptId = checkoutAttemptId;
        Quantity = quantity;
        ReservedAtUtc = reservedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ReserveReason = reserveReason;
        ReserveReference = reserveReference;
        ReservedBy = reservedBy;
        Status = StockReservationStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid InventoryItemId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid CheckoutAttemptId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset ReservedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public string ReserveReason { get; private set; }

    public string ReserveReference { get; private set; }

    public string ReservedBy { get; private set; }

    public StockReservationStatus Status { get; private set; }

    public DateTimeOffset? TerminalAtUtc { get; private set; }

    public string? TerminalActor { get; private set; }

    public string? TerminalReason { get; private set; }

    public string? TerminalReference { get; private set; }

    public Guid? ConsumedMovementId { get; private set; }

    public bool IsEffectiveActiveAt(DateTimeOffset nowUtc)
    {
        InventoryEvidence.EnsureUtc(nowUtc);
        return Status == StockReservationStatus.Active && nowUtc < ExpiresAtUtc;
    }

    internal void ApplyTerminal(
        StockReservationStatus status,
        DateTimeOffset terminalAtUtc,
        string actor,
        string reason,
        string reference,
        Guid? consumedMovementId)
    {
        Status = status;
        TerminalAtUtc = terminalAtUtc;
        TerminalActor = actor;
        TerminalReason = reason;
        TerminalReference = reference;
        ConsumedMovementId = consumedMovementId;
    }
}
