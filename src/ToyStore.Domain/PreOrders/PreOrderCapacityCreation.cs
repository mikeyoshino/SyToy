namespace ToyStore.Domain.PreOrders;

public sealed record PreOrderCapacityCreation(
    PreOrderCapacity Capacity,
    PreOrderCapacityMovement Movement)
{
    internal static PreOrderCapacityCreation Create(
        PreOrderCapacity capacity,
        PreOrderCapacityMovement movement) =>
        new(capacity, movement);
}
