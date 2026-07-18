using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ToyStore.Application.Checkout;

namespace ToyStore.Infrastructure.Payments;

internal sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripePaymentOptions options;
    private readonly StripeClient? client;

    public StripePaymentGateway(IOptions<StripePaymentOptions> optionsAccessor)
    {
        options = optionsAccessor.Value;
        if (!string.IsNullOrWhiteSpace(options.SecretKey))
            client = new StripeClient(options.SecretKey);
    }

    public string PublishableKey => options.PublishableKey;

    public async Task<PaymentSessionResult> CreatePreOrderDepositSessionAsync(
        PaymentSessionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var service = new SessionService(client);
        var unitAmount = decimal.ToInt64(decimal.Round(request.UnitDepositAmount * 100m, 0));
        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            UiMode = "embedded_page",
            CustomerEmail = request.CustomerEmail,
            ReturnUrl = $"{options.ReturnUrlBase.TrimEnd('/')}/checkout/preorder/return?attempt={request.CheckoutAttemptId:D}&session_id={{CHECKOUT_SESSION_ID}}",
            RedirectOnCompletion = "if_required",
            ExpiresAt = request.ExpiresAtUtc.UtcDateTime,
            PaymentMethodTypes = ["card", "promptpay"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = request.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "thb",
                        UnitAmount = unitAmount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"มัดจำ {request.ProductName}",
                        },
                    },
                },
            ],
            Metadata = new Dictionary<string, string>
            {
                ["checkout_attempt_id"] = request.CheckoutAttemptId.ToString("D"),
                ["customer_id"] = request.CustomerId,
                ["purpose"] = "preorder_deposit",
            },
        };
        var session = await service.CreateAsync(createOptions,
            new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
        if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.ClientSecret))
            throw new InvalidOperationException("Stripe did not return an embedded Checkout Session secret.");
        return new(session.Id, session.ClientSecret);
    }

    public async Task<PaymentSessionResult> CreateInStockSessionAsync(
        InStockPaymentSessionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (request.Items.Count == 0)
            throw new ArgumentException("Stripe session requires at least one item.", nameof(request));
        var service = new SessionService(client);
        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            UiMode = "embedded_page",
            CustomerEmail = request.CustomerEmail,
            ReturnUrl = $"{options.ReturnUrlBase.TrimEnd('/')}/checkout/return?attempt={request.CheckoutAttemptId:D}&session_id={{CHECKOUT_SESSION_ID}}",
            RedirectOnCompletion = "if_required",
            ExpiresAt = request.ExpiresAtUtc.UtcDateTime,
            PaymentMethodTypes = ["card", "promptpay"],
            LineItems = request.Items.Select(item => new SessionLineItemOptions
            {
                Quantity = item.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "thb",
                    UnitAmount = decimal.ToInt64(decimal.Round(item.UnitPrice * 100m, 0)),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = item.ProductName },
                },
            }).ToList(),
            Metadata = new Dictionary<string, string>
            {
                ["checkout_attempt_id"] = request.CheckoutAttemptId.ToString("D"),
                ["customer_id"] = request.CustomerId,
                ["purpose"] = "instock_full",
            },
        };
        var session = await service.CreateAsync(createOptions,
            new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
        if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.ClientSecret))
            throw new InvalidOperationException("Stripe did not return an embedded Checkout Session secret.");
        return new(session.Id, session.ClientSecret);
    }

    public PaymentWebhookEvidence VerifyWebhook(string payload, string signature)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        var stripeEvent = EventUtility.ConstructEvent(payload, signature, options.WebhookSecret,
            tolerance: 300, throwOnApiVersionMismatch: false);
        if (stripeEvent.Data.Object is not Session session)
            throw new StripeException("Stripe event does not contain a Checkout Session.");
        Guid? checkoutAttemptId = null;
        if (session.Metadata.TryGetValue("checkout_attempt_id", out var value)
            && Guid.TryParse(value, out var parsed))
            checkoutAttemptId = parsed;
        session.Metadata.TryGetValue("purpose", out var purpose);
        return new(stripeEvent.Id, stripeEvent.Type, session.Id, checkoutAttemptId,
            session.PaymentIntentId, session.AmountTotal ?? 0, session.Currency ?? string.Empty,
            string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase),
            new DateTimeOffset(stripeEvent.Created.ToUniversalTime()), purpose);
    }

    private void EnsureConfigured()
    {
        if (client is null || string.IsNullOrWhiteSpace(options.PublishableKey)
            || string.IsNullOrWhiteSpace(options.ReturnUrlBase))
            throw new InvalidOperationException("Stripe payment configuration is incomplete.");
    }
}
