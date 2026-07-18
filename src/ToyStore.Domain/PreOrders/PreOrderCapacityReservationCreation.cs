namespace ToyStore.Domain.PreOrders;

public sealed record PreOrderCapacityReservationCreation(
    PreOrderCapacityReservation Reservation,
    PreOrderCapacityMovement Movement)
{
    internal static PreOrderCapacityReservationCreation Create(
        PreOrderCapacityReservation reservation,
        PreOrderCapacityMovement movement) =>
        new(reservation, movement);
}
