namespace ToyStore.Domain.Inventory;

public sealed class ReservationTransitionResult
{
    private ReservationTransitionResult(bool changed, StockMovement? movement)
    {
        Changed = changed;
        Movement = movement;
    }

    public bool Changed { get; }

    public StockMovement? Movement { get; }

    internal static ReservationTransitionResult Unchanged() => new(false, null);

    internal static ReservationTransitionResult ChangedWithoutMovement() => new(true, null);

    internal static ReservationTransitionResult ChangedWithMovement(StockMovement movement) =>
        new(true, movement);
}
