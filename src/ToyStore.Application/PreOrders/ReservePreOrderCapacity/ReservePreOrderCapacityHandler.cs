using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders.ReservePreOrderCapacity;

public sealed class ReservePreOrderCapacityHandler(
    IPreOrderCapacityMutationSessionFactory sessionFactory,
    TimeProvider timeProvider)
    : IRequestHandler<ReservePreOrderCapacityCommand, Result<PreOrderCapacityMutationResult>>
{
    public async Task<Result<PreOrderCapacityMutationResult>> Handle(
        ReservePreOrderCapacityCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Pre-order reserve requires an authorized customer.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async token =>
        {
            var locked = await session.LockCapacityAsync(request.CapacityId, request.ProductId, token);
            if (locked is null)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.NotFound);
            }

            var existing = await session.FindMovementAsync(request.OperationId, token);
            if (existing is not null)
            {
                var reservation = await session.FindReservationAsync(request.ReservationId, token);
                if (reservation is null
                    || existing.Type != PreOrderCapacityMovementType.Reserved
                    || existing.CapacityId != request.CapacityId
                    || existing.ProductId != request.ProductId
                    || existing.ReservationId != request.ReservationId
                    || existing.Quantity != request.Quantity
                    || existing.ResultingCapacityVersion != request.ExpectedVersion + 1
                    || existing.Reason != request.Reason.Trim()
                    || existing.Reference != request.Reference.Trim()
                    || existing.Actor != customerId
                    || reservation.CheckoutAttemptId != request.CheckoutAttemptId
                    || reservation.CustomerId != customerId
                    || reservation.Quantity != request.Quantity
                    || reservation.ReservedAtUtc != existing.OccurredAtUtc
                    || reservation.ExpiresAtUtc != existing.OccurredAtUtc + PreOrderCapacityPolicy.ReservationLifetime
                    || reservation.ReserveMovementId != request.OperationId
                    || reservation.ReserveReason != request.Reason.Trim()
                    || reservation.ReserveReference != request.Reference.Trim()
                    || reservation.ReservedBy != customerId)
                {
                    return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.OperationConflict);
                }

                return Result<PreOrderCapacityMutationResult>.Success(
                    PreOrderCapacityMutationResult.From(locked.Capacity, reservation, changed: false));
            }

            if (locked.Capacity.Version != request.ExpectedVersion)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.StaleVersion);
            }

            var allocated = await session.CustomerAllocatedQuantityAsync(request.ProductId, customerId, token);
            if ((long)allocated + request.Quantity > locked.MaxPerCustomer)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.CustomerLimitExceeded);
            }

            try
            {
                var reservedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
                var creation = locked.Capacity.Reserve(
                    request.ReservationId,
                    request.CheckoutAttemptId,
                    customerId,
                    request.Quantity,
                    reservedAtUtc,
                    reservedAtUtc + PreOrderCapacityPolicy.ReservationLifetime,
                    request.OperationId,
                    request.Reason,
                    request.Reference,
                    request.ExpectedVersion,
                    customerId);
                session.Add(creation.Reservation);
                session.Add(creation.Movement);
                return Result<PreOrderCapacityMutationResult>.Success(
                    PreOrderCapacityMutationResult.From(locked.Capacity, creation.Reservation, changed: true));
            }
            catch (PreOrderCapacityRuleException exception)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityMutationSupport.Map(exception));
            }
        }, cancellationToken);
    }
}
