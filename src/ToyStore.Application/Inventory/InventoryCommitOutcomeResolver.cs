using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Inventory;

public sealed class InventoryCommitOutcomeResolver(
    ILogger<InventoryCommitOutcomeResolver> logger)
{
    private static readonly Action<ILogger, string, string, int, Exception?> CommitFailed =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Error,
            new EventId(1, nameof(CommitFailed)),
            "Inventory mutation commit acknowledgement failed for {OperationType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, string, int, Exception?> CommitCancelled =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(2, nameof(CommitCancelled)),
            "Inventory mutation commit acknowledgement was cancelled for {OperationType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, int, Exception?> ResourceCleanupFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(3, nameof(ResourceCleanupFailed)),
            "Inventory mutation persistence resources could not be fully released for {OperationType}; cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, Exception?> OperationCollisionVerificationUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, nameof(OperationCollisionVerificationUnavailable)),
            "Inventory operation collision verification was unavailable for {OperationType}");
    private static readonly Action<ILogger, string, Exception?> OperationCollisionEvidenceInconsistent =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5, nameof(OperationCollisionEvidenceInconsistent)),
            "Inventory operation collision evidence was inconsistent for {OperationType}");

    public async Task<Result<T>> ResolveAsync<T>(
        InventoryMutationExecution<T> execution,
        Func<CancellationToken, Task<InventoryCommitVerificationResult>> verifyCommit,
        Func<InventoryMutationEvidence, T> refreshResult,
        string operationType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(verifyCommit);
        ArgumentNullException.ThrowIfNull(refreshResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        _ = cancellationToken;

        if (execution.CommitOutcome != InventoryCommitOutcome.Indeterminate
            && execution.SafeCleanupFailureTypes.Count > 0)
        {
            ResourceCleanupFailed(
                logger,
                operationType,
                execution.SafeCleanupFailureTypes.Count,
                null);
        }

        if (execution.Result.IsFailure)
        {
            return execution.Result;
        }

        if (execution.CommitOutcome == InventoryCommitOutcome.Committed)
        {
            return execution.Result;
        }

        if (execution.CommitOutcome == InventoryCommitOutcome.DefinitelyRolledBack)
        {
            throw new InvalidOperationException(
                "A definitely rolled-back Inventory mutation cannot return success.");
        }

        var commitFailure = execution.CommitFailure
            ?? throw new InvalidOperationException(
                "An indeterminate Inventory mutation must preserve its commit failure.");
        LogCommitFailure(commitFailure, operationType);

        var verification = await VerifyNonCancellablyAsync(verifyCommit);
        var result = verification.Outcome switch
        {
            InventoryCommitVerification.Committed => execution.Result,
            InventoryCommitVerification.Superseded =>
                Result<T>.Success(refreshResult(verification.AuthoritativeEvidence)),
            InventoryCommitVerification.Inconsistent
                or InventoryCommitVerification.Conflict
                or InventoryCommitVerification.Unavailable =>
                Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown),
            _ => throw new InvalidOperationException(
                "Unknown Inventory commit verification outcome."),
        };

        if (commitFailure.IsCancellation)
        {
            ExceptionDispatchInfo.Capture(commitFailure.OriginalException).Throw();
        }

        return result;
    }

    public async Task<Result<T>> ResolveOperationCollisionAsync<T>(
        Func<CancellationToken, Task<InventoryCommitVerificationResult>> verifyOperation,
        Func<InventoryMutationEvidence, T> unchangedResult,
        Error operationConflict,
        string operationType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifyOperation);
        ArgumentNullException.ThrowIfNull(unchangedResult);
        ArgumentNullException.ThrowIfNull(operationConflict);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        _ = cancellationToken;

        var verification = await VerifyNonCancellablyAsync(verifyOperation);
        if (verification.Outcome == InventoryCommitVerification.Unavailable)
        {
            OperationCollisionVerificationUnavailable(logger, operationType, null);
        }

        if (verification.Outcome == InventoryCommitVerification.Inconsistent)
        {
            OperationCollisionEvidenceInconsistent(logger, operationType, null);
        }

        return verification.Outcome switch
        {
            InventoryCommitVerification.Committed
                or InventoryCommitVerification.Superseded =>
                Result<T>.Success(unchangedResult(verification.AuthoritativeEvidence)),
            InventoryCommitVerification.Conflict =>
                Result<T>.Failure(operationConflict),
            InventoryCommitVerification.Inconsistent
                or InventoryCommitVerification.Unavailable =>
                Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown),
            _ => throw new InvalidOperationException(
                "Unknown Inventory operation collision verification outcome."),
        };
    }

    private static async Task<InventoryCommitVerificationResult> VerifyNonCancellablyAsync(
        Func<CancellationToken, Task<InventoryCommitVerificationResult>> verify)
    {
        try
        {
            return await verify(CancellationToken.None);
        }
        catch
        {
            return InventoryCommitVerificationResult.Unavailable();
        }
    }

    private void LogCommitFailure(
        InventoryCommitFailure commitFailure,
        string operationType)
    {
        var exceptionType = commitFailure.OriginalException.GetType().FullName
            ?? commitFailure.OriginalException.GetType().Name;
        if (commitFailure.IsCancellation)
        {
            CommitCancelled(
                logger,
                operationType,
                exceptionType,
                commitFailure.CleanupFailureTypes.Count,
                null);
            return;
        }

        CommitFailed(
            logger,
            operationType,
            exceptionType,
            commitFailure.CleanupFailureTypes.Count,
            null);
    }
}
