using ToyStore.Application.Common.Models;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Application.Checkout;

public interface IPreOrderCheckoutStore
{
    Task<Result<PreparedPreOrderCheckout>> PrepareAsync(
        PreparePreOrderCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<Result<PreparedPreOrderCheckout>> AttachProviderSessionAsync(
        string customerId,
        Guid checkoutAttemptId,
        string providerSessionId,
        CancellationToken cancellationToken);

    Task<Result<FulfilledPreOrderCheckout>> FulfillAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken);

    Task<Result<PreOrderCheckoutStatusResult>> GetStatusAsync(
        string customerId,
        Guid checkoutAttemptId,
        CancellationToken cancellationToken);

    Task<Result<ExpiredCheckoutResult>> ExpireAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken);
}

public sealed record PreparePreOrderCheckoutRequest(
    Guid CheckoutAttemptId,
    Guid ReservationId,
    Guid ReserveMovementId,
    Guid ProductId,
    int Quantity,
    string CustomerId,
    ShippingAddressSnapshot Address,
    string IdempotencyKey,
    DateTimeOffset NowUtc);

public sealed record PreparedPreOrderCheckout(
    Guid CheckoutAttemptId,
    string CustomerId,
    string IdempotencyKey,
    string ProductName,
    int Quantity,
    decimal FullPrice,
    decimal DepositAmount,
    decimal BalanceAmount,
    decimal PaymentAmount,
    string Currency,
    DateTimeOffset ExpiresAtUtc,
    string? ProviderSessionId);

public sealed record FulfilledPreOrderCheckout(Guid OrderId, string OrderNumber, bool Changed);

public sealed record PreOrderCheckoutStatusResult(
    Guid CheckoutAttemptId,
    string Status,
    string ProductName,
    decimal PaymentAmount,
    string? OrderNumber);
