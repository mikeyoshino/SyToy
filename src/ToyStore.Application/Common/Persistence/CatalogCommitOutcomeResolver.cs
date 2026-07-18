using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Persistence;

public sealed class CatalogCommitOutcomeResolver(
    ILogger<CatalogCommitOutcomeResolver> logger)
{
    private static readonly Action<ILogger, string, string, int, Exception?> CommitFailed =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Error,
            new EventId(1, nameof(CommitFailed)),
            "Catalog mutation commit acknowledgement failed for {EntityType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, string, int, Exception?> CommitCancelled =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(2, nameof(CommitCancelled)),
            "Catalog mutation commit acknowledgement was cancelled for {EntityType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, int, Exception?> ResourceCleanupFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(3, nameof(ResourceCleanupFailed)),
            "Catalog mutation persistence resources could not be fully released for {EntityType}; cleanup failures: {CleanupFailureCount}");

    public async Task<Result<T>> ResolveAsync<T, TAuthoritative>(
        CatalogMutationExecution<T> execution,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        string entityType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(verifyCommit);
        ArgumentNullException.ThrowIfNull(refreshResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        _ = cancellationToken;

        if (execution.CommitOutcome != CatalogCommitOutcome.Indeterminate
            && execution.SafeCleanupFailureTypes.Count > 0)
        {
            ResourceCleanupFailed(
                logger,
                entityType,
                execution.SafeCleanupFailureTypes.Count,
                null);
        }

        if (execution.Result.IsFailure)
        {
            return execution.Result;
        }

        if (execution.CommitOutcome == CatalogCommitOutcome.Committed)
        {
            return execution.Result;
        }

        if (execution.CommitOutcome == CatalogCommitOutcome.DefinitelyRolledBack)
        {
            throw new InvalidOperationException(
                "A definitely rolled-back catalog mutation cannot return success.");
        }

        var commitFailure = execution.CommitFailure
            ?? throw new InvalidOperationException(
                "An indeterminate catalog mutation must preserve its commit failure.");
        LogCommitFailure(commitFailure, entityType);

        CatalogCommitVerification<TAuthoritative> verification;
        try
        {
            verification = await verifyCommit(CancellationToken.None);
        }
        catch
        {
            verification = CatalogCommitVerificationResult.Unavailable<TAuthoritative>();
        }

        Result<T> result = verification.Outcome switch
        {
            CatalogCommitVerification.Committed => execution.Result,
            CatalogCommitVerification.Superseded =>
                Result<T>.Success(refreshResult(verification.AuthoritativeState)),
            CatalogCommitVerification.NotCommitted
                or CatalogCommitVerification.Unavailable
                or CatalogCommitVerification.Inconsistent =>
                Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown),
            _ => throw new InvalidOperationException(
                "Unknown catalog commit verification outcome."),
        };

        if (commitFailure.IsCancellation)
        {
            ExceptionDispatchInfo.Capture(commitFailure.OriginalException).Throw();
        }

        return result;
    }

    private void LogCommitFailure(
        CatalogCommitFailure commitFailure,
        string entityType)
    {
        var exceptionType = commitFailure.OriginalException.GetType().FullName
            ?? commitFailure.OriginalException.GetType().Name;
        if (commitFailure.IsCancellation)
        {
            CommitCancelled(
                logger,
                entityType,
                exceptionType,
                commitFailure.CleanupFailureTypes.Count,
                null);
            return;
        }

        CommitFailed(
            logger,
            entityType,
            exceptionType,
            commitFailure.CleanupFailureTypes.Count,
            null);
    }
}
