using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Inventory;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class InventoryMutationSessionFactory(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IInventoryMutationSessionFactory
{
    public async ValueTask<IInventoryMutationSession> OpenAsync(
        CancellationToken cancellationToken) =>
        new InventoryMutationSession(
            await contextFactory.CreateDbContextAsync(cancellationToken));

    public async Task<InventoryCommitVerificationResult> VerifyCommitAsync(
        InventoryMutationEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(
                cancellationToken);
            var movement = await dbContext.StockMovements
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    current => current.Id == evidence.OperationId,
                    cancellationToken);
            if (movement is null)
            {
                return InventoryCommitVerificationResult.Inconsistent();
            }

            if (!InventoryOperationIntent.FromEvidence(evidence).Matches(movement))
            {
                return InventoryCommitVerificationResult.Conflict();
            }

            if (!MovementEvidenceMatches(evidence, movement))
            {
                return InventoryCommitVerificationResult.Inconsistent();
            }

            var item = await dbContext.InventoryItems
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    current => current.Id == evidence.InventoryItemId
                        && current.ProductId == evidence.ProductId,
                    cancellationToken);
            if (item is null || item.Version < evidence.IntendedVersion)
            {
                return InventoryCommitVerificationResult.Inconsistent();
            }

            var authoritative = InventoryMutationEvidence.Capture(item, movement);
            if (item.Version > evidence.IntendedVersion)
            {
                return InventoryCommitVerificationResult.Superseded(authoritative);
            }

            return InventoryMatches(evidence, item)
                ? InventoryCommitVerificationResult.Committed(authoritative)
                : InventoryCommitVerificationResult.Inconsistent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InventoryCommitVerificationResult.Unavailable();
        }
    }

    private static bool MovementEvidenceMatches(
        InventoryMutationEvidence expected,
        StockMovement actual) =>
        actual.ResultingOnHandQuantity == expected.ResultingOnHandQuantity
        && actual.OccurredAtUtc == expected.OccurredAtUtc
        && actual.ReservationId == expected.ReservationId;

    private static bool InventoryMatches(
        InventoryMutationEvidence expected,
        InventoryItem actual) =>
        actual.OnHandQuantity == expected.IntendedOnHandQuantity
        && actual.HeldQuantity == expected.IntendedHeldQuantity
        && actual.Version == expected.IntendedVersion
        && actual.UpdatedAtUtc == expected.IntendedUpdatedAtUtc
        && actual.UpdatedBy == expected.IntendedUpdatedBy;
}

internal sealed class InventoryMutationSession(ApplicationDbContext dbContext)
    : IInventoryMutationSession
{
    private IDbContextTransaction? transaction;
    private int executionStarted;
    private bool operationActive;
    private bool resourcesDisposed;
    private bool inventoryLockAttempted;
    private Guid lockedInventoryItemId;
    private Guid lockedProductId;
    private long lockedInventoryVersion;

    public async Task<InventoryItem?> LockInventoryAsync(
        Guid inventoryItemId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        if (inventoryLockAttempted)
        {
            throw new InvalidOperationException(
                "The Inventory mutation session can lock only one Inventory item.");
        }

        inventoryLockAttempted = true;
        lockedInventoryItemId = inventoryItemId;
        lockedProductId = productId;
        var matches = await dbContext.InventoryItems
            .FromSqlInterpolated(
            $"""
            SELECT *
            FROM "InventoryItems"
            WHERE "Id" = {inventoryItemId} AND "ProductId" = {productId}
            FOR UPDATE
            """)
            .ToListAsync(cancellationToken);
        var item = matches.SingleOrDefault();
        lockedInventoryVersion = item?.Version ?? 0;
        return item;
    }

    public Task<StockMovement?> FindMovementAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        EnsureInventoryLockAttempted();
        return dbContext.StockMovements
            .AsNoTracking()
            .SingleOrDefaultAsync(movement => movement.Id == operationId, cancellationToken);
    }

    public async Task<StockReservation?> FindReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        EnsureInventoryLockAttempted();
        var reservation = await dbContext.StockReservations.SingleOrDefaultAsync(
            current => current.Id == reservationId,
            cancellationToken);
        if (reservation is null)
        {
            return null;
        }

        if (reservation.InventoryItemId != lockedInventoryItemId
            || reservation.ProductId != lockedProductId)
        {
            throw new InvalidOperationException(
                "Persisted reservation ownership does not match the locked Inventory item.");
        }

        await ValidateReciprocalConsumeEvidenceAsync(reservation, cancellationToken);
        return reservation;
    }

    public void Add(InventoryCreation creation)
    {
        EnsureOperationActive();
        ArgumentNullException.ThrowIfNull(creation);
        dbContext.InventoryItems.Add(creation.Item);
        dbContext.StockMovements.Add(creation.InitialMovement);
    }

    public void Add(StockMovement movement)
    {
        EnsureInventoryLockAttempted();
        ArgumentNullException.ThrowIfNull(movement);
        EnsureLockedOwnership(movement.InventoryItemId, movement.ProductId);
        dbContext.StockMovements.Add(movement);
    }

    public void Add(StockReservation reservation)
    {
        EnsureInventoryLockAttempted();
        ArgumentNullException.ThrowIfNull(reservation);
        EnsureLockedOwnership(reservation.InventoryItemId, reservation.ProductId);
        dbContext.StockReservations.Add(reservation);
    }

    public async Task<InventoryMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.Exchange(ref executionStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The Inventory mutation session can execute only once.");
        }

        InventoryMutationExecution<T>? execution = null;
        Exception? executionException = null;
        IReadOnlyList<Exception> cleanupExceptions;
        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            operationActive = true;
            execution = await ExecuteOperationAsync(operation, cancellationToken);
        }
        catch (Exception exception)
        {
            executionException = exception;
        }
        finally
        {
            operationActive = false;
            cleanupExceptions = await ReleaseResourcesAsync();
        }

        if (executionException is not null)
        {
            RethrowExecutionFailure(executionException, cleanupExceptions);
        }

        return AttachCleanupFailures(execution!, cleanupExceptions);
    }

    public async ValueTask DisposeAsync()
    {
        var cleanupExceptions = await ReleaseResourcesAsync();
        if (cleanupExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
        }

        if (cleanupExceptions.Count > 1)
        {
            throw new AggregateException(
                "The Inventory mutation session could not release its persistence resources.",
                cleanupExceptions);
        }
    }

    private async Task ValidateReciprocalConsumeEvidenceAsync(
        StockReservation reservation,
        CancellationToken cancellationToken)
    {
        if (reservation.Status == StockReservationStatus.Consumed)
        {
            var movement = reservation.ConsumedMovementId.HasValue
                ? await dbContext.StockMovements.AsNoTracking().SingleOrDefaultAsync(
                    current => current.Id == reservation.ConsumedMovementId.Value,
                    cancellationToken)
                : null;
            if (movement is null
                || movement.Type != StockMovementType.ReservationConsumed
                || movement.ReservationId != reservation.Id
                || movement.InventoryItemId != reservation.InventoryItemId
                || movement.ProductId != reservation.ProductId
                || movement.QuantityDelta != -reservation.Quantity
                || movement.OccurredAtUtc != reservation.TerminalAtUtc
                || movement.Actor != reservation.TerminalActor
                || movement.Reason != reservation.TerminalReason
                || movement.Reference != reservation.TerminalReference
                || movement.ResultingInventoryVersion > lockedInventoryVersion)
            {
                throw new InvalidOperationException(
                    "Persisted consumed reservation evidence is not reciprocal.");
            }

            return;
        }

        if (reservation.ConsumedMovementId.HasValue
            || await dbContext.StockMovements.AsNoTracking().AnyAsync(
                movement => movement.ReservationId == reservation.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Persisted non-consumed reservation has consume movement evidence.");
        }
    }

    private async Task<InventoryMutationExecution<T>> ExecuteOperationAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var rollbackAttempted = false;
        try
        {
            var result = await operation(cancellationToken);
            if (result.IsFailure)
            {
                rollbackAttempted = true;
                await RollbackAndClearAsync();
                return new InventoryMutationExecution<T>(
                    result,
                    InventoryCommitOutcome.DefinitelyRolledBack);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            try
            {
                await transaction!.CommitAsync(cancellationToken);
                return new InventoryMutationExecution<T>(
                    result,
                    InventoryCommitOutcome.Committed);
            }
            catch (Exception commitException)
            {
                dbContext.ChangeTracker.Clear();
                return new InventoryMutationExecution<T>(
                    result,
                    InventoryCommitOutcome.Indeterminate,
                    InventoryCommitFailure.Create(commitException));
            }
        }
        catch (Exception exception)
        {
            if (!rollbackAttempted)
            {
                await RollbackAndRethrowAsync(exception);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private async Task RollbackAndClearAsync()
    {
        try
        {
            await transaction!.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task RollbackAndRethrowAsync(Exception originalException)
    {
        try
        {
            await RollbackAndClearAsync();
        }
        catch (Exception rollbackException)
        {
            throw new AggregateException(
                "The Inventory mutation failed and rollback also failed.",
                originalException,
                rollbackException);
        }
    }

    private async Task<IReadOnlyList<Exception>> ReleaseResourcesAsync()
    {
        if (resourcesDisposed)
        {
            return [];
        }

        resourcesDisposed = true;
        var failures = new List<Exception>();
        try
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            transaction = null;
            try
            {
                await dbContext.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static InventoryMutationExecution<T> AttachCleanupFailures<T>(
        InventoryMutationExecution<T> execution,
        IReadOnlyList<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 0)
        {
            return execution;
        }

        var failureTypes = cleanupExceptions
            .Select(exception => exception.GetType().FullName ?? exception.GetType().Name)
            .ToArray();
        if (execution.CommitFailure is not null)
        {
            return new InventoryMutationExecution<T>(
                execution.Result,
                execution.CommitOutcome,
                execution.CommitFailure with
                {
                    CleanupFailureTypes = execution.CommitFailure.CleanupFailureTypes
                        .Concat(failureTypes)
                        .ToArray(),
                });
        }

        return new InventoryMutationExecution<T>(
            execution.Result,
            execution.CommitOutcome,
            cleanupFailureTypes: failureTypes);
    }

    private static void RethrowExecutionFailure(
        Exception executionException,
        IReadOnlyList<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count > 0)
        {
            throw new AggregateException(
                "The Inventory mutation failed and persistence cleanup also failed.",
                new[] { executionException }.Concat(cleanupExceptions));
        }

        ExceptionDispatchInfo.Capture(executionException).Throw();
    }

    private void EnsureOperationActive()
    {
        if (!operationActive)
        {
            throw new InvalidOperationException(
                "Inventory persistence operations require the active once-only transaction.");
        }
    }

    private void EnsureInventoryLockAttempted()
    {
        EnsureOperationActive();
        if (!inventoryLockAttempted)
        {
            throw new InvalidOperationException(
                "Inventory persistence requires the target Inventory lock first.");
        }
    }

    private void EnsureLockedOwnership(Guid inventoryItemId, Guid productId)
    {
        if (inventoryItemId != lockedInventoryItemId || productId != lockedProductId)
        {
            throw new InvalidOperationException(
                "Inventory evidence does not belong to the locked Inventory item.");
        }
    }
}
