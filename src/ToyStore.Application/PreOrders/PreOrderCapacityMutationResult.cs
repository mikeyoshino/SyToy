using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders;

public sealed record PreOrderCapacityMutationResult(
    Guid CapacityId,
    Guid ProductId,
    Guid ReservationId,
    PreOrderCapacityReservationStatus ReservationStatus,
    int RemainingQuantity,
    int HeldQuantity,
    int CommittedQuantity,
    int RetiredQuantity,
    long Version,
    bool Changed)
{
    public static PreOrderCapacityMutationResult From(
        PreOrderCapacity capacity,
        PreOrderCapacityReservation reservation,
        bool changed) =>
        new(
            capacity.Id,
            capacity.ProductId,
            reservation.Id,
            reservation.Status,
            capacity.RemainingQuantity,
            capacity.HeldQuantity,
            capacity.CommittedQuantity,
            capacity.RetiredQuantity,
            capacity.Version,
            changed);
}
