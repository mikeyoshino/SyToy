using ToyStore.Application.Common.Files;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class MediaCleanupEntry
{
    private MediaCleanupEntry()
    {
    }

    private MediaCleanupEntry(
        Guid id,
        MediaCleanupRegistration registration,
        DateTimeOffset observedAtUtc)
    {
        Id = id;
        StorageKey = registration.StorageKey.Value;
        ApplyContext(registration);
        FirstObservedAtUtc = observedAtUtc;
        LastAttemptAtUtc = observedAtUtc;
        AttemptCount = 1;
    }

    public Guid Id { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public MediaCleanupReason Reason { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public DateTimeOffset FirstObservedAtUtc { get; private set; }

    public DateTimeOffset LastAttemptAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    internal static MediaCleanupEntry Create(
        MediaCleanupRegistration registration,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(registration);
        EnsureUtc(observedAtUtc);
        return new MediaCleanupEntry(Guid.NewGuid(), registration, observedAtUtc);
    }

    internal void ObserveAgain(
        MediaCleanupRegistration registration,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(registration);
        EnsureUtc(observedAtUtc);
        if (ResolvedAtUtc is not null)
        {
            throw new InvalidOperationException("A resolved media cleanup entry cannot be observed again.");
        }

        if (!string.Equals(StorageKey, registration.StorageKey.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A cleanup entry cannot change its storage key.");
        }

        if (observedAtUtc < LastAttemptAtUtc)
        {
            throw new InvalidOperationException("Cleanup observation time cannot move backwards.");
        }

        ApplyContext(registration);
        LastAttemptAtUtc = observedAtUtc;
        AttemptCount = checked(AttemptCount + 1);
    }

    private void ApplyContext(MediaCleanupRegistration registration)
    {
        if (!Enum.IsDefined(registration.Reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(registration),
                registration.Reason,
                "Unknown media cleanup reason.");
        }

        StorageKey = !string.IsNullOrWhiteSpace(registration.StorageKey.Value)
            ? registration.StorageKey.Value
            : throw new ArgumentException("A cleanup storage key is required.", nameof(registration));
        EntityType = !string.IsNullOrWhiteSpace(registration.EntityType)
            ? registration.EntityType
            : throw new ArgumentException("A cleanup entity type is required.", nameof(registration));
        EntityId = registration.EntityId != Guid.Empty
            ? registration.EntityId
            : throw new ArgumentException("A cleanup entity ID is required.", nameof(registration));
        Reason = registration.Reason;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Cleanup instants must be UTC.", nameof(value));
        }
    }
}
