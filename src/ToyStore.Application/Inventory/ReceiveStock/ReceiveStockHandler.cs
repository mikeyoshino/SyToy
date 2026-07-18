using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory.ReceiveStock;

public sealed class ReceiveStockHandler(
    IInventoryMutationSessionFactory sessionFactory,
    InventoryCommitOutcomeResolver commitResolver,
    IPersistenceFailureClassifier persistenceFailureClassifier,
    TimeProvider timeProvider)
    : IRequestHandler<ReceiveStockCommand, Result<InventoryMutationResult>>
{
    public async Task<Result<InventoryMutationResult>> Handle(
        ReceiveStockCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before receiving Inventory stock.");
        InventoryMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        InventoryMutationExecution<InventoryMutationResult> execution;
        try
        {
            execution = await session.ExecuteOnceAsync(
                token => MutateAsync(
                    session,
                    request,
                    actor,
                    evidence => intendedEvidence = evidence,
                    token),
                cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateOperation(exception))
        {
            var evidence = RequireEvidence(intendedEvidence);
            return await commitResolver.ResolveOperationCollisionAsync(
                token => sessionFactory.VerifyCommitAsync(evidence, token),
                authoritative => InventoryMutationResult.From(authoritative, changed: false),
                InventoryErrors.OperationConflict,
                "ReceiveStock",
                cancellationToken);
        }

        return await commitResolver.ResolveAsync(
            execution,
            token => sessionFactory.VerifyCommitAsync(
                RequireEvidence(intendedEvidence),
                token),
            authoritative => InventoryMutationResult.From(authoritative, changed: true),
            "ReceiveStock",
            cancellationToken);
    }

    private async Task<Result<InventoryMutationResult>> MutateAsync(
        IInventoryMutationSession session,
        ReceiveStockCommand request,
        string actor,
        Action<InventoryMutationEvidence> captureEvidence,
        CancellationToken cancellationToken)
    {
        var item = await session.LockInventoryAsync(
            request.InventoryItemId,
            request.ProductId,
            cancellationToken);
        if (item is null)
        {
            return Result<InventoryMutationResult>.Failure(InventoryErrors.NotFound);
        }

        var intent = InventoryOperationIntent.Create(
            request.OperationId,
            request.InventoryItemId,
            request.ProductId,
            StockMovementType.Received,
            request.Quantity,
            request.ExpectedVersion,
            request.Reason,
            request.Reference,
            actor);
        var existing = await session.FindMovementAsync(
            request.OperationId,
            cancellationToken);
        if (existing is not null)
        {
            if (!intent.Matches(existing))
            {
                return Result<InventoryMutationResult>.Failure(
                    InventoryErrors.OperationConflict);
            }

            InventoryOperationRetryGuard.EnsureOwningState(item, existing);
            captureEvidence(InventoryMutationEvidence.Capture(item, existing));
            return Result<InventoryMutationResult>.Success(
                InventoryMutationResult.From(item, changed: false));
        }

        if (item.Version != request.ExpectedVersion)
        {
            return Result<InventoryMutationResult>.Failure(InventoryErrors.StaleVersion);
        }

        try
        {
            var movement = item.ReceiveStock(
                request.OperationId,
                request.Quantity,
                request.Reason,
                request.Reference,
                request.ExpectedVersion,
                timeProvider.GetUtcNow().ToUniversalTime(),
                actor);
            session.Add(movement);
            var evidence = InventoryMutationEvidence.Capture(item, movement);
            captureEvidence(evidence);
            return Result<InventoryMutationResult>.Success(
                InventoryMutationResult.From(evidence, changed: true));
        }
        catch (InventoryRuleException exception) when (
            exception.Rule is InventoryRule.ConcurrencyVersionMismatch
                or InventoryRule.QuantityOverflow
                or InventoryRule.ConcurrencyVersionExhausted)
        {
            return exception.Rule switch
            {
                InventoryRule.ConcurrencyVersionMismatch =>
                    Result<InventoryMutationResult>.Failure(InventoryErrors.StaleVersion),
                InventoryRule.QuantityOverflow =>
                    Result<InventoryMutationResult>.Failure(InventoryErrors.QuantityOverflow),
                InventoryRule.ConcurrencyVersionExhausted =>
                    Result<InventoryMutationResult>.Failure(InventoryErrors.VersionExhausted),
                _ => throw new InvalidOperationException(
                    "Unsupported mapped Inventory rule.",
                    exception),
            };
        }
    }

    private bool IsDuplicateOperation(Exception exception) =>
        persistenceFailureClassifier.Classify(exception) is
        {
            Target: PersistenceFailureTarget.StockMovement,
            Kind: PersistenceFailureKind.DuplicateOperation,
        };

    private static InventoryMutationEvidence RequireEvidence(
        InventoryMutationEvidence? evidence) =>
        evidence ?? throw new InvalidOperationException(
            "ReceiveStock persistence reconciliation requires intended evidence.");
}
