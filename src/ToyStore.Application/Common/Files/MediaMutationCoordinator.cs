using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Common.Files;

public sealed class MediaMutationCoordinator(
    IFileStorage fileStorage,
    IMediaReferenceVerifier mediaReferenceVerifier,
    IMediaCleanupRegistry cleanupRegistry,
    ILogger<MediaMutationCoordinator> logger)
{
    private static readonly Action<ILogger, string, string, int, Exception?> CommitAcknowledgementFailed =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Error,
            new EventId(1, nameof(CommitAcknowledgementFailed)),
            "Catalog media mutation commit acknowledgement failed for {EntityType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, int, Exception?> SessionCleanupFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(2, nameof(SessionCleanupFailed)),
            "Catalog media mutation persistence resources could not be fully released for {EntityType}; cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, string, int, Exception?> CommitAcknowledgementCancelled =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            new EventId(3, nameof(CommitAcknowledgementCancelled)),
            "Catalog media mutation commit acknowledgement was cancelled for {EntityType} with {ExceptionType}; resource cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, string, Exception?> StagingDiscardFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(4, nameof(StagingDiscardFailed)),
            "Catalog media staging discard failed for {EntityType} with {ExceptionType}");

    public Task<Result<T>> ExecuteAsync<T>(
        MediaUpload upload,
        ICatalogMutationSession session,
        Func<StagedMedia, CancellationToken, Task<Result<T>>> mutation,
        Func<CancellationToken, Task<CatalogCommitVerification<T>>> verifyCommit,
        MediaMutationContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            upload,
            session,
            mutation,
            verifyCommit,
            static authoritative => authoritative,
            context,
            cancellationToken);

    public Task<Result<T>> ExecuteAsync<T, TAuthoritative>(
        MediaUpload upload,
        ICatalogMutationSession session,
        Func<StagedMedia, CancellationToken, Task<Result<T>>> mutation,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        MediaMutationContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            upload,
            session,
            mutation,
            verifyCommit,
            refreshResult,
            context,
            () => context.PreviousMedia,
            cancellationToken);

    public async Task<Result<T>> ExecuteAsync<T, TAuthoritative>(
        MediaUpload upload,
        ICatalogMutationSession session,
        Func<StagedMedia, CancellationToken, Task<Result<T>>> mutation,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        MediaMutationContext context,
        Func<CatalogMediaReference?> previousMediaAccessor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(verifyCommit);
        ArgumentNullException.ThrowIfNull(refreshResult);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(previousMediaAccessor);

        var stageResult = await fileStorage.StageAsync([upload], cancellationToken);
        if (stageResult.IsFailure)
        {
            return Result<T>.Failure(stageResult.Error, stageResult.ValidationFailures);
        }

        var batch = stageResult.Value;
        if (batch.Media.Count != 1)
        {
            await TryDiscardStagingAsync(batch.BatchToken, context);
            throw new InvalidOperationException(
                "A catalog reference mutation must stage exactly one media item.");
        }

        var stagedMedia = batch.Media[0];
        var storageKey = TrustedMediaStorageKey.From(stagedMedia);
        var commitAttempted = false;
        var commitCompleted = false;
        CatalogMutationExecution<T> execution;

        try
        {
            execution = await session.ExecuteOnceAsync(
                async operationCancellationToken =>
                {
                    var mutationResult = await mutation(
                        stagedMedia,
                        operationCancellationToken);
                    if (mutationResult.IsFailure)
                    {
                        return mutationResult;
                    }

                    commitAttempted = true;
                    await fileStorage.CommitAsync(batch, operationCancellationToken);
                    commitCompleted = true;
                    return mutationResult;
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            var failureContext = WithPreviousMedia(context, previousMediaAccessor);
            await CompensateFailedExecutionAsync(
                batch,
                storageKey,
                commitAttempted,
                commitCompleted,
                failureContext);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        var effectiveContext = WithPreviousMedia(context, previousMediaAccessor);

        if (execution.CommitOutcome != CatalogCommitOutcome.Indeterminate
            && execution.SafeCleanupFailureTypes.Count > 0)
        {
            SessionCleanupFailed(
                logger,
                effectiveContext.EntityType,
                execution.SafeCleanupFailureTypes.Count,
                null);
        }

        if (execution.Result.IsFailure)
        {
            await CompensateFailedExecutionAsync(
                batch,
                storageKey,
                commitAttempted,
                commitCompleted,
                effectiveContext);
            return execution.Result;
        }

        switch (execution.CommitOutcome)
        {
            case CatalogCommitOutcome.Committed:
                await TryDeletePreviousMediaAsync(effectiveContext);
                return execution.Result;

            case CatalogCommitOutcome.DefinitelyRolledBack:
                throw new InvalidOperationException(
                    "A definitely rolled-back session cannot return a successful result.");

            case CatalogCommitOutcome.Indeterminate:
                var commitFailure = execution.CommitFailure
                    ?? throw new InvalidOperationException(
                        "An indeterminate catalog commit must preserve its commit failure.");
                LogCommitFailure(commitFailure, effectiveContext);
                return await ResolveIndeterminateCommitAsync(
                    execution.Result,
                    batch,
                    storageKey,
                    commitAttempted,
                    commitCompleted,
                    verifyCommit,
                    refreshResult,
                    commitFailure,
                    effectiveContext);

            default:
                throw new InvalidOperationException("Unknown catalog commit outcome.");
        }
    }

    private static MediaMutationContext WithPreviousMedia(
        MediaMutationContext context,
        Func<CatalogMediaReference?> previousMediaAccessor) =>
        context with
        {
            PreviousMedia = previousMediaAccessor() ?? context.PreviousMedia,
        };

    private async Task<Result<T>> ResolveIndeterminateCommitAsync<T, TAuthoritative>(
        Result<T> successfulResult,
        StagedMediaBatch batch,
        TrustedMediaStorageKey storageKey,
        bool commitAttempted,
        bool commitCompleted,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        CatalogCommitFailure commitFailure,
        MediaMutationContext context)
    {
        CatalogCommitVerification<TAuthoritative> verification;
        try
        {
            verification = await verifyCommit(CancellationToken.None);
        }
        catch
        {
            verification = CatalogCommitVerificationResult.Unavailable<TAuthoritative>();
        }

        switch (verification.Outcome)
        {
            case CatalogCommitVerification.Committed:
                await TryDeletePreviousMediaAsync(context);
                RethrowCancellation(commitFailure);
                return successfulResult;

            case CatalogCommitVerification.Superseded:
                await TryDeleteCommittedIfUnreferencedAsync(
                    storageKey,
                    context,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                await TryDeletePreviousMediaAsync(context);
                RethrowCancellation(commitFailure);
                return Result<T>.Success(refreshResult(verification.AuthoritativeState));

            case CatalogCommitVerification.NotCommitted:
                await CompensateFailedExecutionAsync(
                    batch,
                    storageKey,
                    commitAttempted,
                    commitCompleted,
                    context);
                RethrowCancellation(commitFailure);
                return CreateCommitOutcomeUnknown<T>();

            case CatalogCommitVerification.Unavailable:
            case CatalogCommitVerification.Inconsistent:
                await TryRecordCleanupAsync(
                    context,
                    storageKey,
                    MediaCleanupReason.CommitOutcomeUnknown);
                RethrowCancellation(commitFailure);
                return CreateCommitOutcomeUnknown<T>();

            default:
                throw new InvalidOperationException("Unknown catalog commit verification.");
        }
    }

    private static Result<T> CreateCommitOutcomeUnknown<T>() =>
        Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown);

    private void LogCommitFailure(
        CatalogCommitFailure commitFailure,
        MediaMutationContext context)
    {
        var exceptionType = commitFailure.OriginalException.GetType().FullName
            ?? commitFailure.OriginalException.GetType().Name;
        var cleanupFailureCount = commitFailure.CleanupFailureTypes.Count;
        if (commitFailure.IsCancellation)
        {
            CommitAcknowledgementCancelled(
                logger,
                context.EntityType,
                exceptionType,
                cleanupFailureCount,
                null);
            return;
        }

        CommitAcknowledgementFailed(
            logger,
            context.EntityType,
            exceptionType,
            cleanupFailureCount,
            null);
    }

    private static void RethrowCancellation(CatalogCommitFailure commitFailure)
    {
        if (commitFailure.IsCancellation)
        {
            ExceptionDispatchInfo.Capture(commitFailure.OriginalException).Throw();
        }
    }

    private async Task CompensateFailedExecutionAsync(
        StagedMediaBatch batch,
        TrustedMediaStorageKey storageKey,
        bool commitAttempted,
        bool commitCompleted,
        MediaMutationContext context)
    {
        if (!commitCompleted)
        {
            await TryDiscardStagingAsync(batch.BatchToken, context);
        }

        if (commitAttempted)
        {
            await TryDeleteCommittedIfUnreferencedAsync(
                storageKey,
                context,
                MediaCleanupReason.ReferenceVerificationUnavailable);
        }
    }

    private Task TryDeletePreviousMediaAsync(MediaMutationContext context) =>
        context.PreviousMedia is null
            ? Task.CompletedTask
            : TryDeleteCommittedIfUnreferencedAsync(
                TrustedMediaStorageKey.From(context.PreviousMedia),
                context,
                MediaCleanupReason.ReferenceVerificationUnavailable);

    private async Task TryDeleteCommittedIfUnreferencedAsync(
        TrustedMediaStorageKey storageKey,
        MediaMutationContext context,
        MediaCleanupReason unavailableReason)
    {
        var firstCheck = await TryVerifyReferenceAsync(storageKey);
        if (firstCheck == MediaReferenceVerification.Referenced)
        {
            return;
        }

        if (firstCheck == MediaReferenceVerification.Unavailable)
        {
            await TryRecordCleanupAsync(context, storageKey, unavailableReason);
            return;
        }

        var immediateDeleteGuard = await TryVerifyReferenceAsync(storageKey);
        if (immediateDeleteGuard == MediaReferenceVerification.Referenced)
        {
            return;
        }

        if (immediateDeleteGuard == MediaReferenceVerification.Unavailable)
        {
            await TryRecordCleanupAsync(context, storageKey, unavailableReason);
            return;
        }

        try
        {
            await fileStorage.DeleteCommittedAsync(
                [storageKey.Value],
                CancellationToken.None);
        }
        catch
        {
            await TryRecordCleanupAsync(
                context,
                storageKey,
                MediaCleanupReason.DeleteFailed);
        }
    }

    private async Task<MediaReferenceVerification> TryVerifyReferenceAsync(
        TrustedMediaStorageKey storageKey)
    {
        try
        {
            return await mediaReferenceVerifier.VerifyAsync(
                storageKey,
                CancellationToken.None);
        }
        catch
        {
            return MediaReferenceVerification.Unavailable;
        }
    }

    private async Task TryDiscardStagingAsync(
        string batchToken,
        MediaMutationContext context)
    {
        try
        {
            await fileStorage.DiscardStagingAsync(batchToken, CancellationToken.None);
        }
        catch (Exception exception)
        {
            StagingDiscardFailed(
                logger,
                context.EntityType,
                exception.GetType().FullName ?? exception.GetType().Name,
                null);
        }
    }

    private async Task TryRecordCleanupAsync(
        MediaMutationContext context,
        TrustedMediaStorageKey storageKey,
        MediaCleanupReason reason)
    {
        try
        {
            await cleanupRegistry.RecordAsync(
                MediaCleanupRegistration.Create(context, storageKey, reason),
                CancellationToken.None);
        }
        catch
        {
            // Cleanup bookkeeping must never reverse an already committed mutation.
        }
    }
}
