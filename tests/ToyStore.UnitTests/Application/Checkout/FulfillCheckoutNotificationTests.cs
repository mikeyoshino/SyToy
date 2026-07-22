using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Checkout;
using ToyStore.Application.Checkout.FulfillInStockCheckout;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Notifications;

namespace ToyStore.UnitTests.Application.Checkout;

public sealed class FulfillCheckoutNotificationTests
{
    [Fact]
    public async Task CommittedOrderRemainsSuccessfulWhenNotificationDispatchThrows()
    {
        var orderId = Guid.NewGuid();
        var store = new StubInStockCheckoutStore(new(orderId, "SY-TEST", true));
        var dispatcher = new StubDispatcher { Exception = new HttpRequestException("provider unavailable") };
        var handler = new FulfillInStockCheckoutHandler(
            store,
            dispatcher,
            NullLogger<FulfillInStockCheckoutHandler>.Instance);

        var result = await handler.Handle(
            new FulfillInStockCheckoutCommand(Evidence()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value.OrderId);
        Assert.Equal(1, dispatcher.CallCount);
    }

    [Fact]
    public async Task WebhookReplayStillDispatchesSoFailedDeliveryCanRetry()
    {
        var orderId = Guid.NewGuid();
        var store = new StubInStockCheckoutStore(new(orderId, "SY-TEST", false));
        var dispatcher = new StubDispatcher();
        var handler = new FulfillInStockCheckoutHandler(
            store,
            dispatcher,
            NullLogger<FulfillInStockCheckoutHandler>.Instance);

        var result = await handler.Handle(
            new FulfillInStockCheckoutCommand(Evidence()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Changed);
        Assert.Equal(1, dispatcher.CallCount);
    }

    private static PaymentWebhookEvidence Evidence() => new(
        "evt_test", "checkout.session.completed", "cs_test", Guid.NewGuid(), "pi_test",
        10000, "thb", true, DateTimeOffset.UtcNow, "instock_full");

    private sealed class StubDispatcher : IOrderPlacedNotificationDispatcher
    {
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }

        public Task DispatchAsync(Guid orderId, string orderNumber, CancellationToken cancellationToken)
        {
            CallCount++;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class StubInStockCheckoutStore(FulfilledInStockCheckout fulfilled)
        : IInStockCheckoutStore
    {
        public Task<Result<FulfilledInStockCheckout>> FulfillAsync(
            PaymentWebhookEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<FulfilledInStockCheckout>.Success(fulfilled));

        public Task<Result<PreparedInStockCheckout>> PrepareAsync(
            PrepareInStockCheckoutRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<PreparedInStockCheckout>> AttachProviderSessionAsync(
            string customerId,
            Guid checkoutAttemptId,
            string providerSessionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<InStockCheckoutStatusResult>> GetStatusAsync(
            string customerId,
            Guid checkoutAttemptId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<ExpiredCheckoutResult>> ExpireAsync(
            PaymentWebhookEvidence evidence,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
