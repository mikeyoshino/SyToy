using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToyStore.Application.Characters;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CharacterMutationSessionFactory(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ICharacterMutationSessionFactory
{
    public async ValueTask<ICharacterMutationSession> OpenAsync(
        CancellationToken cancellationToken) =>
        new CharacterMutationSession(
            await contextFactory.CreateDbContextAsync(cancellationToken));

    public async Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
        CharacterMutationEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(
                cancellationToken);
            var persisted = await dbContext.Characters
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    character => character.Id == evidence.Id,
                    cancellationToken);

            if (persisted is null)
            {
                return CatalogCommitVerificationResult
                    .NotCommitted<CharacterMutationEvidence>();
            }

            return persisted.UniverseId == evidence.UniverseId
                && persisted.Name == evidence.Name
                && persisted.NormalizedName == evidence.NormalizedName
                    ? CatalogCommitVerificationResult.Committed(
                        CharacterMutationEvidence.Capture(persisted))
                    : CatalogCommitVerificationResult
                        .Inconsistent<CharacterMutationEvidence>();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CatalogCommitVerificationResult
                .Unavailable<CharacterMutationEvidence>();
        }
    }
}

internal sealed class CharacterMutationSession(ApplicationDbContext dbContext)
    : ICharacterMutationSession
{
    private IDbContextTransaction? transaction;
    private int executionStarted;
    private bool operationActive;
    private Guid? lockedUniverseId;
    private bool resourcesDisposed;

    public async Task<bool> LockActiveUniverseAsync(
        Guid universeId,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        if (lockedUniverseId.HasValue)
        {
            if (lockedUniverseId.Value != universeId)
            {
                throw new InvalidOperationException(
                    "A Character mutation session can lock only one Universe.");
            }

            return await IsUniverseActiveAsync(universeId, cancellationToken);
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Universes\" WHERE \"Id\" = {universeId} FOR UPDATE",
            cancellationToken);
        lockedUniverseId = universeId;
        return await IsUniverseActiveAsync(universeId, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        Guid universeId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        EnsureUniverseLock(universeId);
        return dbContext.Characters.AnyAsync(
            character => character.UniverseId == universeId
                && character.NormalizedName == normalizedName,
            cancellationToken);
    }

    public void Add(Character character)
    {
        EnsureOperationActive();
        ArgumentNullException.ThrowIfNull(character);
        EnsureUniverseLock(character.UniverseId);
        dbContext.Characters.Add(character);
    }

    public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Interlocked.Exchange(ref executionStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The Character mutation session can execute only once.");
        }

        CatalogMutationExecution<T>? execution = null;
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
                "The Character mutation session could not release its persistence resources.",
                cleanupExceptions);
        }
    }

    private Task<bool> IsUniverseActiveAsync(
        Guid universeId,
        CancellationToken cancellationToken) =>
        dbContext.Universes
            .AsNoTracking()
            .AnyAsync(
                universe => universe.Id == universeId
                    && universe.Status == CatalogReferenceStatus.Active,
                cancellationToken);

    private async Task<CatalogMutationExecution<T>> ExecuteOperationAsync<T>(
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
                return new CatalogMutationExecution<T>(
                    result,
                    CatalogCommitOutcome.DefinitelyRolledBack);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            try
            {
                await transaction!.CommitAsync(cancellationToken);
                return new CatalogMutationExecution<T>(
                    result,
                    CatalogCommitOutcome.Committed);
            }
            catch (Exception commitException)
            {
                dbContext.ChangeTracker.Clear();
                return new CatalogMutationExecution<T>(
                    result,
                    CatalogCommitOutcome.Indeterminate,
                    CatalogCommitFailure.Create(commitException));
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

    private async Task<IReadOnlyList<Exception>> ReleaseResourcesAsync()
    {
        if (resourcesDisposed)
        {
            return [];
        }

        resourcesDisposed = true;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        catch (Exception transactionDisposeException)
        {
            cleanupExceptions.Add(transactionDisposeException);
        }
        finally
        {
            transaction = null;
            try
            {
                await dbContext.DisposeAsync();
            }
            catch (Exception contextDisposeException)
            {
                cleanupExceptions.Add(contextDisposeException);
            }
        }

        return cleanupExceptions;
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
                "The Character mutation failed and its rollback also failed.",
                originalException,
                rollbackException);
        }

        ExceptionDispatchInfo.Capture(originalException).Throw();
    }

    private static CatalogMutationExecution<T> AttachCleanupFailures<T>(
        CatalogMutationExecution<T> execution,
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
            return execution with
            {
                CommitFailure = execution.CommitFailure with
                {
                    CleanupFailureTypes = execution.CommitFailure.CleanupFailureTypes
                        .Concat(failureTypes)
                        .ToArray(),
                },
            };
        }

        return execution with { CleanupFailureTypes = failureTypes };
    }

    private static void RethrowExecutionFailure(
        Exception executionException,
        IReadOnlyList<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count > 0)
        {
            throw new AggregateException(
                "The Character mutation failed and persistence resource cleanup also failed.",
                new[] { executionException }.Concat(cleanupExceptions));
        }

        ExceptionDispatchInfo.Capture(executionException).Throw();
    }

    private void EnsureOperationActive()
    {
        if (!operationActive)
        {
            throw new InvalidOperationException(
                "Character persistence operations are available only inside ExecuteOnceAsync.");
        }
    }

    private void EnsureUniverseLock(Guid universeId)
    {
        if (lockedUniverseId != universeId)
        {
            throw new InvalidOperationException(
                "Character name checks and inserts require the target Universe row lock.");
        }
    }
}
