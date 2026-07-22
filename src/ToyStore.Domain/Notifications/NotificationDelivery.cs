namespace ToyStore.Domain.Notifications;

public enum NotificationDeliveryStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
}

public sealed class NotificationDelivery
{
    private NotificationDelivery()
    {
        Type = RecipientKey = IdempotencyKey = Payload = null!;
    }

    private NotificationDelivery(
        Guid id,
        Guid orderId,
        string type,
        string recipientKey,
        string idempotencyKey,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrderId = orderId;
        Type = type;
        RecipientKey = recipientKey;
        IdempotencyKey = idempotencyKey;
        Payload = payload;
        Status = NotificationDeliveryStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Type { get; private set; }
    public string RecipientKey { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string Payload { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? SafeProviderResponse { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptedAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public static NotificationDelivery Create(
        Guid id,
        Guid orderId,
        string type,
        string recipientKey,
        string idempotencyKey,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || orderId == Guid.Empty)
            throw new ArgumentException("Notification identity is required.");
        if (string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(recipientKey)
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Notification delivery details are required.");
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return new(id, orderId, type.Trim(), recipientKey.Trim(), idempotencyKey.Trim(),
            payload.Trim(), createdAtUtc);
    }

    public bool TryBeginAttempt(DateTimeOffset attemptedAtUtc, TimeSpan abandonedAttemptAfter)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            abandonedAttemptAfter,
            TimeSpan.Zero);
        if (Status == NotificationDeliveryStatus.Sent) return false;
        if (Status == NotificationDeliveryStatus.Sending
            && LastAttemptedAtUtc is not null
            && attemptedAtUtc - LastAttemptedAtUtc < abandonedAttemptAfter)
            return false;

        Status = NotificationDeliveryStatus.Sending;
        Attempts++;
        LastAttemptedAtUtc = attemptedAtUtc;
        SafeProviderResponse = null;
        return true;
    }

    public void MarkSent(string safeProviderResponse, DateTimeOffset deliveredAtUtc)
    {
        EnsureUtc(deliveredAtUtc, nameof(deliveredAtUtc));
        if (Status != NotificationDeliveryStatus.Sending)
            throw new InvalidOperationException("Only an active notification attempt can be completed.");
        Status = NotificationDeliveryStatus.Sent;
        SafeProviderResponse = NormalizeResponse(safeProviderResponse);
        DeliveredAtUtc = deliveredAtUtc;
    }

    public void MarkFailed(string safeProviderResponse)
    {
        if (Status != NotificationDeliveryStatus.Sending)
            throw new InvalidOperationException("Only an active notification attempt can fail.");
        Status = NotificationDeliveryStatus.Failed;
        SafeProviderResponse = NormalizeResponse(safeProviderResponse);
    }

    private static string NormalizeResponse(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "provider-error" : value.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Notification timestamps must be UTC.", parameterName);
    }
}
