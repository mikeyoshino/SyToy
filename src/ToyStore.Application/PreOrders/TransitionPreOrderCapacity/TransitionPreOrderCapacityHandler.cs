using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Application.PreOrders.TransitionPreOrderCapacity;

public sealed class TransitionPreOrderCapacityHandler(
    IPreOrderCapacityMutationSessionFactory sessionFactory,
    TimeProvider timeProvider)
    : IRequestHandler<TransitionPreOrderCapacityCommand, Result<PreOrderCapacityMutationResult>>
{
    public async Task<Result<PreOrderCapacityMutationResult>> Handle(
        TransitionPreOrderCapacityCommand request,
        CancellationToken cancellationToken)
    {
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Pre-order transition requires an authorized actor.");
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        return await session.ExecuteOnceAsync(async token =>
        {
            var locked = await session.LockCapacityAsync(request.CapacityId, request.ProductId, token);
            if (locked is null)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.NotFound);
            }

            var reservation = await session.FindReservationAsync(request.ReservationId, token);
            if (reservation is null)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.ReservationNotFound);
            }

            if (request.Action == PreOrderCapacityAction.CancelCustomer
                && !string.Equals(reservation.CustomerId, actor, StringComparison.Ordinal))
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.ReservationNotFound);
            }

            var existing = await session.FindMovementAsync(request.OperationId, token);
            if (existing is not null
                && !MatchesExistingIntent(existing, reservation, request, actor))
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityErrors.OperationConflict);
            }

            try
            {
                var now = timeProvider.GetUtcNow().ToUniversalTime();
                var result = request.Action switch
                {
                    PreOrderCapacityAction.Consume => locked.Capacity.ConsumeReservation(
                        reservation, request.OperationId, request.Reason, request.Reference,
                        request.ExpectedVersion, now, actor),
                    PreOrderCapacityAction.Release => locked.Capacity.ReleaseReservation(
                        reservation, request.OperationId, request.Reason, request.Reference,
                        request.ExpectedVersion, now, actor),
                    PreOrderCapacityAction.Expire => locked.Capacity.ExpireReservation(
                        reservation, request.OperationId, request.Reason, request.Reference,
                        request.ExpectedVersion, now, actor),
                    PreOrderCapacityAction.CancelCustomer => locked.Capacity.CancelReservation(
                        reservation, request.OperationId, PreOrderCancellationKind.Customer,
                        request.Reason, request.Reference, request.ExpectedVersion, now, actor),
                    PreOrderCapacityAction.CancelAdminOrSupplier => locked.Capacity.CancelReservation(
                        reservation, request.OperationId, PreOrderCancellationKind.AdminOrSupplier,
                        request.Reason, request.Reference, request.ExpectedVersion, now, actor),
                    PreOrderCapacityAction.CancelBalanceOverdue => locked.Capacity.CancelReservation(
                        reservation, request.OperationId, PreOrderCancellationKind.BalanceOverdue,
                        request.Reason, request.Reference, request.ExpectedVersion, now, actor),
                    _ => throw new InvalidOperationException("Unsupported pre-order capacity action."),
                };
                if (result.Changed)
                {
                    session.Add(AssertMovement(result));
                }

                return Result<PreOrderCapacityMutationResult>.Success(
                    PreOrderCapacityMutationResult.From(locked.Capacity, reservation, result.Changed));
            }
            catch (PreOrderCapacityRuleException exception)
            {
                return Result<PreOrderCapacityMutationResult>.Failure(PreOrderCapacityMutationSupport.Map(exception));
            }
        }, cancellationToken);
    }

    private static PreOrderCapacityMovement AssertMovement(PreOrderCapacityTransitionResult result) =>
        result.Movement
        ?? throw new InvalidOperationException("Changed pre-order transition requires a movement.");

    private static bool MatchesExistingIntent(
        PreOrderCapacityMovement movement,
        PreOrderCapacityReservation reservation,
        TransitionPreOrderCapacityCommand request,
        string actor) =>
        movement.CapacityId == request.CapacityId
        && movement.ProductId == request.ProductId
        && movement.ReservationId == request.ReservationId
        && movement.Quantity == reservation.Quantity
        && movement.ResultingCapacityVersion == request.ExpectedVersion + 1
        && movement.Reason == request.Reason.Trim()
        && movement.Reference == request.Reference.Trim()
        && movement.Actor == actor
        && ActionMatchesMovement(request.Action, movement.Type, reservation.CancellationKind);

    private static bool ActionMatchesMovement(
        PreOrderCapacityAction action,
        PreOrderCapacityMovementType movementType,
        PreOrderCancellationKind? cancellationKind) => action switch
        {
            PreOrderCapacityAction.Consume =>
                movementType == PreOrderCapacityMovementType.ReservationConsumed,
            PreOrderCapacityAction.Release =>
                movementType == PreOrderCapacityMovementType.Released,
            PreOrderCapacityAction.Expire =>
                movementType == PreOrderCapacityMovementType.Expired,
            PreOrderCapacityAction.CancelCustomer =>
                IsCancellationMovement(movementType)
                && cancellationKind == PreOrderCancellationKind.Customer,
            PreOrderCapacityAction.CancelAdminOrSupplier =>
                IsCancellationMovement(movementType)
                && cancellationKind == PreOrderCancellationKind.AdminOrSupplier,
            PreOrderCapacityAction.CancelBalanceOverdue =>
                IsCancellationMovement(movementType)
                && cancellationKind == PreOrderCancellationKind.BalanceOverdue,
            _ => false,
        };

    private static bool IsCancellationMovement(PreOrderCapacityMovementType movementType) =>
        movementType is PreOrderCapacityMovementType.CancellationReopened
            or PreOrderCapacityMovementType.CancellationRetired;
}
