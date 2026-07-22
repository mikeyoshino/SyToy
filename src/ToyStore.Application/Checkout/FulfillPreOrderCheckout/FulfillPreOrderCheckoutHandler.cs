using MediatR;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Notifications;

namespace ToyStore.Application.Checkout.FulfillPreOrderCheckout;

public sealed partial class FulfillPreOrderCheckoutHandler(
    IPreOrderCheckoutStore repository,
    IOrderPlacedNotificationDispatcher notificationDispatcher,
    ILogger<FulfillPreOrderCheckoutHandler> logger)
    : IRequestHandler<FulfillPreOrderCheckoutCommand, Result<FulfilledPreOrderCheckout>>
{
    public async Task<Result<FulfilledPreOrderCheckout>> Handle(
        FulfillPreOrderCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var result = await repository.FulfillAsync(request.Evidence, cancellationToken);
        if (result.IsFailure) return result;

        try
        {
            await notificationDispatcher.DispatchAsync(
                result.Value.OrderId,
                result.Value.OrderNumber,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogNotificationFailure(logger, exception, result.Value.OrderId);
        }

        return result;
    }

    [LoggerMessage(
        EventId = 7302,
        Level = LogLevel.Error,
        Message = "Order notification dispatch failed for OrderId {OrderId}.")]
    private static partial void LogNotificationFailure(
        ILogger logger,
        Exception exception,
        Guid orderId);
}
