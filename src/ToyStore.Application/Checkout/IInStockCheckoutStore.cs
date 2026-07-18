using ToyStore.Application.Common.Models;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Application.Checkout;

public interface IInStockCheckoutStore
{
    Task<Result<PreparedInStockCheckout>> PrepareAsync(
        PrepareInStockCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<Result<PreparedInStockCheckout>> AttachProviderSessionAsync(
        string customerId,
        Guid checkoutAttemptId,
        string providerSessionId,
        CancellationToken cancellationToken);

    Task<Result<FulfilledInStockCheckout>> FulfillAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken);

    Task<Result<InStockCheckoutStatusResult>> GetStatusAsync(
        string customerId,
        Guid checkoutAttemptId,
        CancellationToken cancellationToken);

    Task<Result<ExpiredCheckoutResult>> ExpireAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken);
}

public sealed record PrepareInStockCheckoutRequest(
    Guid CheckoutAttemptId,
    string CustomerId,
    ShippingAddressSnapshot Address,
    string IdempotencyKey,
    DateTimeOffset NowUtc);

public sealed record PreparedInStockCheckout(
    Guid CheckoutAttemptId,
    string CustomerId,
    string IdempotencyKey,
    IReadOnlyList<PreparedInStockCheckoutItem> Items,
    decimal ShippingAmount,
    decimal PaymentAmount,
    string Currency,
    DateTimeOffset ExpiresAtUtc,
    string? ProviderSessionId);

public sealed record PreparedInStockCheckoutItem(
    Guid ProductId,
    string ProductName,
    string PrimaryImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineAmount);

public sealed record FulfilledInStockCheckout(Guid OrderId, string OrderNumber, bool Changed);

public sealed record InStockCheckoutStatusResult(
    Guid CheckoutAttemptId,
    string Status,
    decimal PaymentAmount,
    string? OrderNumber);

public sealed record ExpiredCheckoutResult(Guid CheckoutAttemptId, bool Changed);
