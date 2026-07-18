namespace ToyStore.Domain.PreOrders;

public sealed class PreOrderCapacityTransitionResult
{
    private PreOrderCapacityTransitionResult(
        bool changed,
        PreOrderCapacityMovement? movement)
    {
        Changed = changed;
        Movement = movement;
    }

    public bool Changed { get; }

    public PreOrderCapacityMovement? Movement { get; }

    internal static PreOrderCapacityTransitionResult Unchanged() => new(false, null);

    internal static PreOrderCapacityTransitionResult ChangedWith(
        PreOrderCapacityMovement movement) =>
        new(true, movement);
}
