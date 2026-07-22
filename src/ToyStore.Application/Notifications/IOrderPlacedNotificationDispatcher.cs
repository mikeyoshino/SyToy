namespace ToyStore.Application.Notifications;

public interface IOrderPlacedNotificationDispatcher
{
    Task DispatchAsync(
        Guid orderId,
        string orderNumber,
        CancellationToken cancellationToken);
}
