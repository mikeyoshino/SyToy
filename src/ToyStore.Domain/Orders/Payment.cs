namespace ToyStore.Domain.Orders;

public enum PaymentPurpose { Deposit, Full, Balance, Refund }

public sealed class Payment
{
    private Payment() { Currency = ProviderSessionId = ProviderPaymentReference = ProviderEventId = null!; }
    private Payment(Guid id, Guid orderId, Guid checkoutAttemptId, PaymentPurpose purpose,
        decimal amount, string currency, string sessionId, string paymentReference,
        string eventId, DateTimeOffset paidAtUtc)
    {
        Id = id; OrderId = orderId; CheckoutAttemptId = checkoutAttemptId; Purpose = purpose;
        Amount = amount; Currency = currency; ProviderSessionId = sessionId;
        ProviderPaymentReference = paymentReference; ProviderEventId = eventId; PaidAtUtc = paidAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CheckoutAttemptId { get; private set; }
    public PaymentPurpose Purpose { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string ProviderSessionId { get; private set; }
    public string ProviderPaymentReference { get; private set; }
    public string ProviderEventId { get; private set; }
    public DateTimeOffset PaidAtUtc { get; private set; }

    public static Payment CreateDeposit(Guid id, Guid orderId, Guid checkoutAttemptId, decimal amount,
        string currency, string sessionId, string paymentReference, string eventId, DateTimeOffset paidAtUtc)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || checkoutAttemptId == Guid.Empty || amount <= 0)
            throw new ArgumentException("Payment identity and amount are required.");
        return new(id, orderId, checkoutAttemptId, PaymentPurpose.Deposit, amount, currency.Trim().ToUpperInvariant(),
            sessionId.Trim(), paymentReference.Trim(), eventId.Trim(), paidAtUtc);
    }

    public static Payment CreateFull(Guid id, Guid orderId, Guid checkoutAttemptId, decimal amount,
        string currency, string sessionId, string paymentReference, string eventId, DateTimeOffset paidAtUtc)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || checkoutAttemptId == Guid.Empty || amount <= 0)
            throw new ArgumentException("Payment identity and amount are required.");
        return new(id, orderId, checkoutAttemptId, PaymentPurpose.Full, amount,
            currency.Trim().ToUpperInvariant(), sessionId.Trim(), paymentReference.Trim(),
            eventId.Trim(), paidAtUtc);
    }
}
