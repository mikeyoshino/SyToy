using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

public abstract record ProductMediaPlanSlot;

public sealed record RetainedProductMediaSlot(Guid ProductImageId) : ProductMediaPlanSlot;

public sealed record UploadProductMediaSlot(MediaUpload Upload) : ProductMediaPlanSlot;

public abstract record ResolvedProductMediaSlot;

public sealed record ResolvedRetainedProductMediaSlot(Guid ProductImageId)
    : ResolvedProductMediaSlot;

public sealed record ResolvedUploadProductMediaSlot(StagedMedia Media)
    : ResolvedProductMediaSlot;

public sealed class ProductMediaSnapshot
{
    private ProductMediaSnapshot(IReadOnlyList<TrustedProductMediaSnapshot> media) =>
        Media = media;

    internal IReadOnlyList<TrustedProductMediaSnapshot> Media { get; }

    public static ProductMediaSnapshot Capture(IReadOnlyCollection<ProductImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var snapshot = images
            .Select(image => image is null
                ? throw new ArgumentException(
                    "A Product media snapshot cannot contain null images.",
                    nameof(images))
                : TrustedProductMediaSnapshot.Capture(image))
            .OrderBy(image => image.SortOrder)
            .ToArray();
        return new ProductMediaSnapshot(snapshot);
    }
}

public sealed class ProductMediaMutationState
{
    private ProductMediaMutationState(
        ProductMediaSnapshot before,
        ProductMediaSnapshot after)
    {
        Before = before;
        After = after;
    }

    internal ProductMediaSnapshot Before { get; }

    internal ProductMediaSnapshot After { get; }

    public static ProductMediaMutationState Capture(
        ProductMediaSnapshot before,
        IReadOnlyCollection<ProductImage> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        return new ProductMediaMutationState(before, ProductMediaSnapshot.Capture(after));
    }
}

public sealed record ProductMediaMutationContext
{
    public ProductMediaMutationContext(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Product media mutation requires a Product identity.",
                nameof(productId));
        }

        ProductId = productId;
    }

    public Guid ProductId { get; }

    internal MediaMutationContext CleanupContext => new("Product", ProductId, null);
}

internal sealed record TrustedProductMediaSnapshot(
    Guid Id,
    TrustedMediaStorageKey StorageKey,
    string PublicRelativeUrl,
    TrustedMediaStorageKey? ThumbnailStorageKey,
    string? ThumbnailPublicRelativeUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary)
{
    internal static TrustedProductMediaSnapshot Capture(ProductImage image) =>
        new(
            image.Id,
            TrustedMediaStorageKey.From(image),
            image.PublicRelativeUrl,
            image.ThumbnailStorageKey is null
                ? null
                : TrustedMediaStorageKey.FromThumbnail(image),
            image.ThumbnailPublicRelativeUrl,
            image.AltText,
            image.SortOrder,
            image.IsPrimary);
}

public sealed partial class ProductMediaMutationCoordinator(
    IFileStorage fileStorage,
    IMediaReferenceVerifier mediaReferenceVerifier,
    IMediaCleanupRegistry cleanupRegistry,
    ILogger<ProductMediaMutationCoordinator> logger)
{
    private static readonly Action<ILogger, string, int, Exception?> CommitAcknowledgementFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(1, nameof(CommitAcknowledgementFailed)),
            "Product media mutation commit acknowledgement failed with {ExceptionType}; cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, string, int, Exception?> CommitAcknowledgementCancelled =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2, nameof(CommitAcknowledgementCancelled)),
            "Product media mutation commit acknowledgement was cancelled with {ExceptionType}; cleanup failures: {CleanupFailureCount}");
    private static readonly Action<ILogger, int, Exception?> SessionCleanupFailed =
        LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(3, nameof(SessionCleanupFailed)),
            "Product media mutation persistence cleanup failed in {CleanupFailureCount} resource operations");
    private static readonly Action<ILogger, string, Exception?> StagingDiscardFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4, nameof(StagingDiscardFailed)),
            "Product media staging discard failed with {ExceptionType}");

    public async Task<Result<T>> ExecuteAsync<T, TAuthoritative>(
        IReadOnlyList<ProductMediaPlanSlot> orderedSlots,
        ICatalogMutationSession session,
        Func<IReadOnlyList<ResolvedProductMediaSlot>, CancellationToken, Task<Result<T>>> mutation,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        ProductMediaMutationContext context,
        Func<ProductMediaMutationState> mediaStateAccessor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedSlots);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(verifyCommit);
        ArgumentNullException.ThrowIfNull(refreshResult);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mediaStateAccessor);

        var slotSnapshot = orderedSlots.ToArray();
        ValidatePlan(slotSnapshot);
        var uploadSnapshot = slotSnapshot
            .OfType<UploadProductMediaSlot>()
            .Select(slot => new MediaUpload(
                slot.Upload.Content,
                slot.Upload.ContentType,
                generateProductThumbnail: true))
            .ToArray();

        StagedMediaBatch? batch = null;
        IReadOnlyList<StagedMedia> stagedMedia = [];
        IReadOnlyList<TrustedMediaStorageKey> newKeys = [];
        if (uploadSnapshot.Length > 0)
        {
            var stageResult = await fileStorage.StageAsync(uploadSnapshot, cancellationToken);
            if (stageResult.IsFailure)
            {
                return Result<T>.Failure(stageResult.Error, stageResult.ValidationFailures);
            }

            batch = stageResult.Value;
            if (!TryValidateBatch(batch, uploadSnapshot.Length, out newKeys))
            {
                await TryDiscardStagingAsync(batch.BatchToken);
                throw new InvalidOperationException(
                    "The staged Product media batch descriptor is invalid.");
            }

            stagedMedia = batch.Media;
        }

        var resolvedSlots = ResolveSlots(slotSnapshot, stagedMedia);
        var commitAttempted = false;
        var commitCompleted = false;
        IReadOnlyList<TrustedMediaStorageKey> unusedNewKeys = [];
        IReadOnlyList<TrustedMediaStorageKey> removedOldKeys = [];
        CatalogMutationExecution<T> execution;
        try
        {
            execution = await session.ExecuteOnceAsync(
                async operationCancellationToken =>
                {
                    var result = await mutation(resolvedSlots, operationCancellationToken);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    var analysis = AnalyzeMutationState(
                        slotSnapshot,
                        resolvedSlots,
                        mediaStateAccessor());
                    unusedNewKeys = analysis.UnusedNewKeys;
                    removedOldKeys = analysis.RemovedOldKeys;
                    if (batch is not null)
                    {
                        commitAttempted = true;
                        await fileStorage.CommitAsync(batch, operationCancellationToken);
                        commitCompleted = true;
                    }

                    return result;
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            await CompensateFailedExecutionAsync(
                batch,
                newKeys,
                commitAttempted,
                commitCompleted,
                context.CleanupContext);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        if (execution.CommitOutcome != CatalogCommitOutcome.Indeterminate
            && execution.SafeCleanupFailureTypes.Count > 0)
        {
            SessionCleanupFailed(logger, execution.SafeCleanupFailureTypes.Count, null);
        }

        if (execution.Result.IsFailure)
        {
            await CompensateFailedExecutionAsync(
                batch,
                newKeys,
                commitAttempted,
                commitCompleted,
                context.CleanupContext);
            return execution.Result;
        }

        switch (execution.CommitOutcome)
        {
            case CatalogCommitOutcome.Committed:
                await ProcessKeysIfUnreferencedAsync(
                    unusedNewKeys,
                    context.CleanupContext,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                await ProcessKeysIfUnreferencedAsync(
                    removedOldKeys,
                    context.CleanupContext,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                return execution.Result;

            case CatalogCommitOutcome.DefinitelyRolledBack:
                await CompensateFailedExecutionAsync(
                    batch,
                    newKeys,
                    commitAttempted,
                    commitCompleted,
                    context.CleanupContext);
                throw new InvalidOperationException(
                    "A definitely rolled-back Product session cannot return success.");

            case CatalogCommitOutcome.Indeterminate:
                return await ResolveIndeterminateAsync(
                    execution,
                    batch,
                    newKeys,
                    unusedNewKeys,
                    removedOldKeys,
                    commitAttempted,
                    commitCompleted,
                    verifyCommit,
                    refreshResult,
                    context.CleanupContext);

            default:
                throw new InvalidOperationException("Unknown catalog commit outcome.");
        }
    }

    private async Task<Result<T>> ResolveIndeterminateAsync<T, TAuthoritative>(
        CatalogMutationExecution<T> execution,
        StagedMediaBatch? batch,
        IReadOnlyList<TrustedMediaStorageKey> newKeys,
        IReadOnlyList<TrustedMediaStorageKey> unusedNewKeys,
        IReadOnlyList<TrustedMediaStorageKey> removedOldKeys,
        bool commitAttempted,
        bool commitCompleted,
        Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
        Func<TAuthoritative, T> refreshResult,
        MediaMutationContext context)
    {
        var commitFailure = execution.CommitFailure
            ?? throw new InvalidOperationException(
                "An indeterminate Product commit must preserve its commit failure.");
        LogCommitFailure(commitFailure);

        CatalogCommitVerification<TAuthoritative> verification;
        try
        {
            verification = await verifyCommit(CancellationToken.None);
        }
        catch
        {
            verification = CatalogCommitVerificationResult.Unavailable<TAuthoritative>();
        }

        Result<T> result;
        switch (verification.Outcome)
        {
            case CatalogCommitVerification.Committed:
                await ProcessKeysIfUnreferencedAsync(
                    unusedNewKeys,
                    context,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                await ProcessKeysIfUnreferencedAsync(
                    removedOldKeys,
                    context,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                result = execution.Result;
                break;

            case CatalogCommitVerification.Superseded:
                await ProcessKeysIfUnreferencedAsync(
                    newKeys,
                    context,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                await ProcessKeysIfUnreferencedAsync(
                    removedOldKeys,
                    context,
                    MediaCleanupReason.ReferenceVerificationUnavailable);
                result = Result<T>.Success(refreshResult(verification.AuthoritativeState));
                break;

            case CatalogCommitVerification.NotCommitted:
                await CompensateFailedExecutionAsync(
                    batch,
                    newKeys,
                    commitAttempted,
                    commitCompleted,
                    context);
                result = Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown);
                break;

            case CatalogCommitVerification.Unavailable:
            case CatalogCommitVerification.Inconsistent:
                foreach (var key in newKeys)
                {
                    await TryRecordCleanupAsync(
                        context,
                        key,
                        MediaCleanupReason.CommitOutcomeUnknown);
                }

                result = Result<T>.Failure(PersistenceErrors.CommitOutcomeUnknown);
                break;

            default:
                throw new InvalidOperationException("Unknown catalog commit verification.");
        }

        if (commitFailure.IsCancellation)
        {
            ExceptionDispatchInfo.Capture(commitFailure.OriginalException).Throw();
        }

        return result;
    }

    private void LogCommitFailure(CatalogCommitFailure commitFailure)
    {
        var type = commitFailure.OriginalException.GetType().FullName
            ?? commitFailure.OriginalException.GetType().Name;
        if (commitFailure.IsCancellation)
        {
            CommitAcknowledgementCancelled(
                logger,
                type,
                commitFailure.CleanupFailureTypes.Count,
                null);
        }
        else
        {
            CommitAcknowledgementFailed(
                logger,
                type,
                commitFailure.CleanupFailureTypes.Count,
                null);
        }
    }

    private async Task CompensateFailedExecutionAsync(
        StagedMediaBatch? batch,
        IReadOnlyList<TrustedMediaStorageKey> newKeys,
        bool commitAttempted,
        bool commitCompleted,
        MediaMutationContext context)
    {
        if (batch is not null && !commitCompleted)
        {
            await TryDiscardStagingAsync(batch.BatchToken);
        }

        if (commitAttempted)
        {
            await ProcessKeysIfUnreferencedAsync(
                newKeys,
                context,
                MediaCleanupReason.ReferenceVerificationUnavailable);
        }
    }

    private async Task ProcessKeysIfUnreferencedAsync(
        IEnumerable<TrustedMediaStorageKey> keys,
        MediaMutationContext context,
        MediaCleanupReason unavailableReason)
    {
        foreach (var key in keys)
        {
            await TryDeleteIfUnreferencedAsync(key, context, unavailableReason);
        }
    }

    private async Task TryDeleteIfUnreferencedAsync(
        TrustedMediaStorageKey key,
        MediaMutationContext context,
        MediaCleanupReason unavailableReason)
    {
        var first = await TryVerifyReferenceAsync(key);
        if (first == MediaReferenceVerification.Referenced)
        {
            return;
        }

        if (first == MediaReferenceVerification.Unavailable)
        {
            await TryRecordCleanupAsync(context, key, unavailableReason);
            return;
        }

        var deleteGuard = await TryVerifyReferenceAsync(key);
        if (deleteGuard == MediaReferenceVerification.Referenced)
        {
            return;
        }

        if (deleteGuard == MediaReferenceVerification.Unavailable)
        {
            await TryRecordCleanupAsync(context, key, unavailableReason);
            return;
        }

        try
        {
            await fileStorage.DeleteCommittedAsync([key.Value], CancellationToken.None);
        }
        catch
        {
            await TryRecordCleanupAsync(context, key, MediaCleanupReason.DeleteFailed);
        }
    }

    private async Task<MediaReferenceVerification> TryVerifyReferenceAsync(
        TrustedMediaStorageKey key)
    {
        try
        {
            return await mediaReferenceVerifier.VerifyAsync(key, CancellationToken.None);
        }
        catch
        {
            return MediaReferenceVerification.Unavailable;
        }
    }

    private async Task TryDiscardStagingAsync(string batchToken)
    {
        try
        {
            await fileStorage.DiscardStagingAsync(batchToken, CancellationToken.None);
        }
        catch (Exception exception)
        {
            StagingDiscardFailed(
                logger,
                exception.GetType().FullName ?? exception.GetType().Name,
                null);
        }
    }

    private async Task TryRecordCleanupAsync(
        MediaMutationContext context,
        TrustedMediaStorageKey key,
        MediaCleanupReason reason)
    {
        try
        {
            await cleanupRegistry.RecordAsync(
                MediaCleanupRegistration.Create(context, key, reason),
                CancellationToken.None);
        }
        catch
        {
            // Cleanup bookkeeping never reverses or retries the primary Product mutation.
        }
    }

    private static void ValidatePlan(ProductMediaPlanSlot[] slots)
    {
        if (slots.Length > Product.MaximumImageCount || slots.Any(slot => slot is null))
        {
            throw new ArgumentException("The ordered Product media plan is invalid.", nameof(slots));
        }

        var retained = slots.OfType<RetainedProductMediaSlot>().ToArray();
        if (retained.Any(slot => slot.ProductImageId == Guid.Empty)
            || retained.Select(slot => slot.ProductImageId).Distinct().Count() != retained.Length)
        {
            throw new ArgumentException(
                "Retained Product images must have distinct identities.",
                nameof(slots));
        }

        var uploads = slots.OfType<UploadProductMediaSlot>().ToArray();
        if (uploads.Any(slot => slot.Upload is null)
            || uploads.Select(slot => slot.Upload).Distinct(ReferenceEqualityComparer.Instance).Count()
                != uploads.Length)
        {
            throw new ArgumentException(
                "Every Product media upload must be present exactly once.",
                nameof(slots));
        }

        if (retained.Length + uploads.Length != slots.Length)
        {
            throw new ArgumentException("The Product media plan contains an unknown slot.", nameof(slots));
        }
    }

    private static List<ResolvedProductMediaSlot> ResolveSlots(
        ProductMediaPlanSlot[] slots,
        IReadOnlyList<StagedMedia> stagedMedia)
    {
        var result = new List<ResolvedProductMediaSlot>(slots.Length);
        var uploadIndex = 0;
        foreach (var slot in slots)
        {
            switch (slot)
            {
                case RetainedProductMediaSlot retained:
                    result.Add(new ResolvedRetainedProductMediaSlot(retained.ProductImageId));
                    break;
                case UploadProductMediaSlot:
                    result.Add(new ResolvedUploadProductMediaSlot(stagedMedia[uploadIndex++]));
                    break;
                default:
                    throw new InvalidOperationException("Unknown Product media plan slot.");
            }
        }

        return result;
    }

    private static MediaStateAnalysis AnalyzeMutationState(
        ProductMediaPlanSlot[] plan,
        List<ResolvedProductMediaSlot> resolved,
        ProductMediaMutationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateSnapshot(state.Before.Media, "before");
        ValidateSnapshot(state.After.Media, "after");

        var beforeById = state.Before.Media.ToDictionary(image => image.Id);
        var usedNew = new HashSet<string>(StringComparer.Ordinal);
        var afterIndex = 0;
        for (var planIndex = 0; planIndex < plan.Length; planIndex++)
        {
            switch (plan[planIndex])
            {
                case RetainedProductMediaSlot retained:
                    if (afterIndex >= state.After.Media.Count
                        || state.After.Media[afterIndex].Id != retained.ProductImageId
                        || !beforeById.TryGetValue(retained.ProductImageId, out var previous)
                        || !SameRetainedMedia(previous, state.After.Media[afterIndex]))
                    {
                        throw new InvalidOperationException(
                            "The locked Product media state does not match its retained-image plan.");
                    }

                    afterIndex++;
                    break;

                case UploadProductMediaSlot:
                    var staged = ((ResolvedUploadProductMediaSlot)resolved[planIndex]).Media;
                    if (afterIndex < state.After.Media.Count
                        && state.After.Media[afterIndex].StorageKey.Value == staged.StorageKey
                        && state.After.Media[afterIndex].PublicRelativeUrl == staged.PublicRelativeUrl)
                    {
                        usedNew.Add(staged.StorageKey);
                        if (staged.ThumbnailStorageKey is not null)
                        {
                            usedNew.Add(staged.ThumbnailStorageKey);
                        }
                        afterIndex++;
                    }

                    break;
            }
        }

        if (afterIndex != state.After.Media.Count)
        {
            throw new InvalidOperationException(
                "The locked Product media state contains images outside its ordered plan.");
        }

        var newKeys = resolved
            .OfType<ResolvedUploadProductMediaSlot>()
            .SelectMany(slot => StagedKeys(slot.Media))
            .ToArray();
        var afterKeys = state.After.Media
            .SelectMany(SnapshotKeys)
            .Select(key => key.Value)
            .ToHashSet(StringComparer.Ordinal);
        return new MediaStateAnalysis(
            newKeys.Where(key => !usedNew.Contains(key.Value)).ToArray(),
            state.Before.Media
                .SelectMany(SnapshotKeys)
                .Where(key => !afterKeys.Contains(key.Value))
                .ToArray());
    }

    private static void ValidateSnapshot(
        IReadOnlyList<TrustedProductMediaSnapshot> media,
        string stateName)
    {
        var allKeys = media.SelectMany(SnapshotKeys).Select(key => key.Value).ToArray();
        if (media.Count > Product.MaximumImageCount
            || media.Select(image => image.Id).Distinct().Count() != media.Count
            || allKeys.Distinct(StringComparer.Ordinal).Count() != allKeys.Length
            || media.Any(image => (image.ThumbnailStorageKey is null)
                != (image.ThumbnailPublicRelativeUrl is null)))
        {
            throw new InvalidOperationException(
                $"The locked Product {stateName} media snapshot is invalid.");
        }

        for (var index = 0; index < media.Count; index++)
        {
            if (media[index].Id == Guid.Empty
                || media[index].SortOrder != index
                || media[index].IsPrimary != (index == 0))
            {
                throw new InvalidOperationException(
                    $"The locked Product {stateName} media order is invalid.");
            }
        }
    }

    private static bool SameRetainedMedia(
        TrustedProductMediaSnapshot before,
        TrustedProductMediaSnapshot after) =>
        before.Id == after.Id
        && before.StorageKey == after.StorageKey
        && before.PublicRelativeUrl == after.PublicRelativeUrl
        && before.ThumbnailStorageKey == after.ThumbnailStorageKey
        && before.ThumbnailPublicRelativeUrl == after.ThumbnailPublicRelativeUrl
        && before.AltText == after.AltText;

    private static bool TryValidateBatch(
        StagedMediaBatch batch,
        int expectedCount,
        out IReadOnlyList<TrustedMediaStorageKey> keys)
    {
        var result = new List<TrustedMediaStorageKey>(batch.Media.Count * 2);
        var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        if (!BatchTokenPattern().IsMatch(batch.BatchToken)
            || batch.Media.Count != expectedCount
            || batch.Media.Count is < 1 or > Product.MaximumImageCount)
        {
            keys = [];
            return false;
        }

        foreach (var media in batch.Media)
        {
            if (media is null)
            {
                keys = [];
                return false;
            }

            var match = StorageKeyPattern().Match(media.StorageKey);
            var expectedContentType = match.Success
                ? match.Groups["extension"].Value switch
                {
                    "jpg" => "image/jpeg",
                    "png" => "image/png",
                    "webp" => "image/webp",
                    _ => string.Empty,
                }
                : string.Empty;
            if (!match.Success
                || !string.Equals(media.BatchToken, batch.BatchToken, StringComparison.Ordinal)
                || !string.Equals(match.Groups["batch"].Value, batch.BatchToken, StringComparison.Ordinal)
                || !string.Equals(
                    media.PublicRelativeUrl,
                    $"/media/{media.StorageKey}",
                    StringComparison.Ordinal)
                || !string.Equals(
                    media.ContentType,
                    expectedContentType,
                    StringComparison.Ordinal)
                || media.Length <= 0
                || !uniqueKeys.Add(media.StorageKey)
                || !TryValidateThumbnail(media, batch.BatchToken, uniqueKeys))
            {
                keys = [];
                return false;
            }

            result.Add(TrustedMediaStorageKey.From(media));
            result.Add(TrustedMediaStorageKey.FromThumbnail(media));
        }

        keys = result;
        return true;
    }

    private static bool TryValidateThumbnail(
        StagedMedia media,
        string batchToken,
        HashSet<string> uniqueKeys)
    {
        var match = StorageKeyPattern().Match(media.ThumbnailStorageKey ?? string.Empty);
        return match.Success
            && match.Groups["extension"].Value == "webp"
            && match.Groups["batch"].Value == batchToken
            && media.ThumbnailPublicRelativeUrl == $"/media/{media.ThumbnailStorageKey}"
            && media.ThumbnailLength is > 0
            && uniqueKeys.Add(media.ThumbnailStorageKey!);
    }

    private static IEnumerable<TrustedMediaStorageKey> StagedKeys(StagedMedia media)
    {
        yield return TrustedMediaStorageKey.From(media);
        if (media.ThumbnailStorageKey is not null)
        {
            yield return TrustedMediaStorageKey.FromThumbnail(media);
        }
    }

    private static IEnumerable<TrustedMediaStorageKey> SnapshotKeys(TrustedProductMediaSnapshot image)
    {
        yield return image.StorageKey;
        if (image.ThumbnailStorageKey is not null)
        {
            yield return image.ThumbnailStorageKey;
        }
    }

    [GeneratedRegex("\\A[a-f0-9]{32}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex BatchTokenPattern();

    [GeneratedRegex(
        "\\A(?<batch>[a-f0-9]{32})/[a-f0-9]{32}\\.(?<extension>jpg|png|webp)\\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex StorageKeyPattern();

    private sealed record MediaStateAnalysis(
        IReadOnlyList<TrustedMediaStorageKey> UnusedNewKeys,
        IReadOnlyList<TrustedMediaStorageKey> RemovedOldKeys);
}
