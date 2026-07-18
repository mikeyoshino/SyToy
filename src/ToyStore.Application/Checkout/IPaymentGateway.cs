namespace ToyStore.Application.Checkout;

public interface IPaymentGateway
{
    string PublishableKey { get; }

    Task<PaymentSessionResult> CreatePreOrderDepositSessionAsync(
        PaymentSessionRequest request,
        CancellationToken cancellationToken);

    Task<PaymentSessionResult> CreateInStockSessionAsync(
        InStockPaymentSessionRequest request,
        CancellationToken cancellationToken);

    PaymentWebhookEvidence VerifyWebhook(string payload, string signature);
}

public sealed record PaymentSessionRequest(
    Guid CheckoutAttemptId,
    string IdempotencyKey,
    string CustomerId,
    string CustomerEmail,
    string ProductName,
    int Quantity,
    decimal UnitDepositAmount,
    DateTimeOffset ExpiresAtUtc);

public sealed record PaymentSessionResult(string SessionId, string ClientSecret);

public sealed record InStockPaymentSessionRequest(
    Guid CheckoutAttemptId,
    string IdempotencyKey,
    string CustomerId,
    string CustomerEmail,
    IReadOnlyList<InStockPaymentLine> Items,
    DateTimeOffset ExpiresAtUtc);

public sealed record InStockPaymentLine(string ProductName, int Quantity, decimal UnitPrice);

public sealed record PaymentWebhookEvidence(
    string EventId,
    string EventType,
    string SessionId,
    Guid? CheckoutAttemptId,
    string? PaymentReference,
    long AmountTotalMinor,
    string Currency,
    bool IsPaid,
    DateTimeOffset OccurredAtUtc,
    string? Purpose = null);
