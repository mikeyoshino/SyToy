using ToyStore.Domain.Notifications;

namespace ToyStore.UnitTests.Domain.Notifications;

public sealed class NotificationDeliveryTests
{
    [Fact]
    public void DeliveryTracksAttemptsFailureRetryAndSuccess()
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), "OrderPlaced.Telegram", "-100123",
            "telegram:order-placed:one", "safe payload", now);

        Assert.True(delivery.TryBeginAttempt(now, TimeSpan.FromMinutes(5)));
        delivery.MarkFailed("telegram-error:500");
        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.Attempts);

        Assert.True(delivery.TryBeginAttempt(now.AddMinutes(1), TimeSpan.FromMinutes(5)));
        delivery.MarkSent("telegram-message:42", now.AddMinutes(1));

        Assert.Equal(NotificationDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(2, delivery.Attempts);
        Assert.False(delivery.TryBeginAttempt(now.AddMinutes(10), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ActiveAttemptCannotBeClaimedUntilItsLeaseIsAbandoned()
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = NotificationDelivery.Create(
            Guid.NewGuid(), Guid.NewGuid(), "OrderPlaced.Telegram", "-100123",
            "telegram:order-placed:two", "safe payload", now);

        Assert.True(delivery.TryBeginAttempt(now, TimeSpan.FromMinutes(5)));
        Assert.False(delivery.TryBeginAttempt(now.AddMinutes(4), TimeSpan.FromMinutes(5)));
        Assert.True(delivery.TryBeginAttempt(now.AddMinutes(5), TimeSpan.FromMinutes(5)));
        Assert.Equal(2, delivery.Attempts);
    }
}
