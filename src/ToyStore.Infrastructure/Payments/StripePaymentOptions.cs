namespace ToyStore.Infrastructure.Payments;

public sealed class StripePaymentOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string ReturnUrlBase { get; set; } = "http://localhost:5141";
}
