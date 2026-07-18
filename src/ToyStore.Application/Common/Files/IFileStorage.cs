using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Files;

public interface IFileStorage
{
    Task<Result<StagedMediaBatch>> StageAsync(
        IReadOnlyCollection<MediaUpload> uploads,
        CancellationToken cancellationToken);

    Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken);

    Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken);

    Task DeleteCommittedAsync(
        IReadOnlyCollection<string> storageKeys,
        CancellationToken cancellationToken);

    Task<StoredMediaRead?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task CleanupStagingAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken);
}
