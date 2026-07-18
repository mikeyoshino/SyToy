using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class UniverseMutationSessionFactory(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IUniverseMutationSessionFactory
{
    public async ValueTask<IUniverseMutationSession> OpenAsync(
        CancellationToken cancellationToken) =>
        new UniverseMutationSession(
            await contextFactory.CreateDbContextAsync(cancellationToken));

    public async Task<CatalogCommitVerification<UniverseMutationEvidence>> VerifyCommitAsync(
        UniverseMutationEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(
                cancellationToken);
            var persisted = await dbContext.Universes
                .AsNoTracking()
                .SingleOrDefaultAsync(universe => universe.Id == evidence.Id, cancellationToken);

            if (persisted is null || persisted.Version < evidence.IntendedVersion)
            {
                return CatalogCommitVerificationResult.NotCommitted<UniverseMutationEvidence>();
            }

            if (persisted.Version > evidence.IntendedVersion)
            {
                return CatalogCommitVerificationResult.Superseded(
                    UniverseMutationEvidence.Capture(persisted));
            }

            return persisted.DisplayName == evidence.DisplayName
                && persisted.EnglishName == evidence.EnglishName
                && persisted.Slug.Value == evidence.Slug
                && persisted.Logo?.StorageKey == evidence.LogoStorageKey
                && persisted.Status == evidence.Status
                    ? CatalogCommitVerificationResult.Committed(
                        UniverseMutationEvidence.Capture(persisted))
                    : CatalogCommitVerificationResult.Inconsistent<UniverseMutationEvidence>();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CatalogCommitVerificationResult.Unavailable<UniverseMutationEvidence>();
        }
    }
}

internal sealed class UniverseMutationSession(ApplicationDbContext dbContext)
    : IUniverseMutationSession
{
    private IDbContextTransaction? transaction;
    private int executionStarted;
    private bool operationActive;
    private bool mutationLockAcquired;
    private bool resourcesDisposed;

    public async Task AcquireMutationLockAsync(CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        if (mutationLockAcquired)
        {
            return;
        }

        await new CatalogReferenceMutationLock(dbContext)
            .AcquireUniverseAsync(cancellationToken);
        mutationLockAcquired = true;
    }

    public async Task<Universe?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Universes\" WHERE \"Id\" = {id} FOR UPDATE",
            cancellationToken);
        return await dbContext.Universes.SingleOrDefaultAsync(
            universe => universe.Id == id,
            cancellationToken);
    }

    public Task<bool> DisplayNameExistsAsync(
        string normalizedDisplayName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        EnsureMutationLockAcquired();
        return dbContext.Universes.AnyAsync(
            universe => universe.NormalizedDisplayName == normalizedDisplayName
                && (!excludedId.HasValue || universe.Id != excludedId.Value),
            cancellationToken);
    }

    public Task<bool> EnglishNameExistsAsync(
        string normalizedEnglishName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        EnsureMutationLockAcquired();
        return dbContext.Universes.AnyAsync(
            universe => universe.NormalizedEnglishName == normalizedEnglishName
                && (!excludedId.HasValue || universe.Id != excludedId.Value),
            cancellationToken);
    }

    public Task<CatalogSlug> AllocateSlugAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureOperationActive();
        EnsureMutationLockAcquired();
        return new CatalogSlugAllocator(dbContext).AllocateUniverseForLockedMutationAsync(
            englishName,
            excludedId,
            cancellationToken);
    }

    public void Add(Universe universe)
    {
        EnsureOperationActive();
        ArgumentNullException.ThrowIfNull(universe);
        dbContext.Universes.Add(universe);
    }

    public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Interlocked.Exchange(ref executionStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The Universe mutation session can execute only once.");
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
                "The Universe mutation session could not release its persistence resources.",
                cleanupExceptions);
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
                "The Universe mutation failed and persistence resource cleanup also failed.",
                new[] { executionException }.Concat(cleanupExceptions));
        }

        ExceptionDispatchInfo.Capture(executionException).Throw();
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
                "The Universe mutation failed and its rollback also failed.",
                originalException,
                rollbackException);
        }

        ExceptionDispatchInfo.Capture(originalException).Throw();
    }

    private void EnsureOperationActive()
    {
        if (!operationActive)
        {
            throw new InvalidOperationException(
                "Universe persistence operations are available only inside ExecuteOnceAsync.");
        }
    }

    private void EnsureMutationLockAcquired()
    {
        if (!mutationLockAcquired)
        {
            throw new InvalidOperationException(
                "Universe name checks and slug allocation require the mutation lock.");
        }
    }
}
