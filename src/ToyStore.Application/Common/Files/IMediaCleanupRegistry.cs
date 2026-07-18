namespace ToyStore.Application.Common.Files;

public interface IMediaCleanupRegistry
{
    Task RecordAsync(
        MediaCleanupRegistration registration,
        CancellationToken cancellationToken);
}

public enum MediaCleanupReason
{
    CommitOutcomeUnknown = 1,
    ReferenceVerificationUnavailable = 2,
    DeleteFailed = 3,
}

public sealed class MediaCleanupRegistration
{
    private MediaCleanupRegistration(
        string entityType,
        Guid entityId,
        TrustedMediaStorageKey storageKey,
        MediaCleanupReason reason)
    {
        EntityType = entityType;
        EntityId = entityId;
        StorageKey = storageKey;
        Reason = reason;
    }

    public string EntityType { get; }

    public Guid EntityId { get; }

    public TrustedMediaStorageKey StorageKey { get; }

    public MediaCleanupReason Reason { get; }

    public static MediaCleanupRegistration Create(
        MediaMutationContext context,
        TrustedMediaStorageKey storageKey,
        MediaCleanupReason reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(storageKey);
        return new MediaCleanupRegistration(
            context.EntityType,
            context.EntityId,
            storageKey,
            reason);
    }
}
