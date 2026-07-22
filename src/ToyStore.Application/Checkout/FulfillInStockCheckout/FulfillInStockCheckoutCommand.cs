using MediatR;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Notifications;

namespace ToyStore.Application.Checkout.FulfillInStockCheckout;

public sealed record FulfillInStockCheckoutCommand(PaymentWebhookEvidence Evidence)
    : IRequest<Result<FulfilledInStockCheckout>>;

public sealed partial class FulfillInStockCheckoutHandler(
    IInStockCheckoutStore checkoutStore,
    IOrderPlacedNotificationDispatcher notificationDispatcher,
    ILogger<FulfillInStockCheckoutHandler> logger)
    : IRequestHandler<FulfillInStockCheckoutCommand, Result<FulfilledInStockCheckout>>
{
    public async Task<Result<FulfilledInStockCheckout>> Handle(
        FulfillInStockCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        var result = await checkoutStore.FulfillAsync(request.Evidence, cancellationToken);
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
        EventId = 7301,
        Level = LogLevel.Error,
        Message = "Order notification dispatch failed for OrderId {OrderId}.")]
    private static partial void LogNotificationFailure(
        ILogger logger,
        Exception exception,
        Guid orderId);
}
