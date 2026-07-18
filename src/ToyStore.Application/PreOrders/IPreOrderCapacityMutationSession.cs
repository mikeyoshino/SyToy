using ToyStore.Application.Common.Models;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders;

public interface IPreOrderCapacityMutationSessionFactory
{
    ValueTask<IPreOrderCapacityMutationSession> OpenAsync(CancellationToken cancellationToken);
}

public interface IPreOrderCapacityMutationSession : IAsyncDisposable
{
    Task<Result<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken);

    Task<LockedPreOrderCapacity?> LockCapacityAsync(
        Guid capacityId,
        Guid productId,
        CancellationToken cancellationToken);

    Task<PreOrderCapacityReservation?> FindReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<PreOrderCapacityMovement?> FindMovementAsync(
        Guid movementId,
        CancellationToken cancellationToken);

    Task<int> CustomerAllocatedQuantityAsync(
        Guid productId,
        string customerId,
        CancellationToken cancellationToken);

    void Add(PreOrderCapacityReservation reservation);

    void Add(PreOrderCapacityMovement movement);
}

public sealed record LockedPreOrderCapacity(
    PreOrderCapacity Capacity,
    int MaxPerCustomer);
