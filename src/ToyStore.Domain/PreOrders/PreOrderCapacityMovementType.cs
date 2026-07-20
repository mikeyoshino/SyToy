namespace ToyStore.Domain.PreOrders;

public enum PreOrderCapacityMovementType
{
    InitialCapacity,
    Reserved,
    Released,
    Expired,
    ReservationConsumed,
    CancellationReopened,
    CancellationRetired,
    CapacityIncreased,
    CapacityDecreased,
}
