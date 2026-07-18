using MediatR;
using Stripe;
using ToyStore.Application.Checkout;
using ToyStore.Application.Checkout.FulfillPreOrderCheckout;
using ToyStore.Application.Checkout.FulfillInStockCheckout;
using ToyStore.Application.Checkout.ExpireCheckout;

namespace ToyStore.Web.Payments;

internal static class StripeWebhookEndpoints
{
    internal static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/stripe", HandleAsync)
            .DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IPaymentGateway paymentGateway,
        ISender sender,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = request.Headers["Stripe-Signature"].ToString();
        PaymentWebhookEvidence evidence;
        try
        {
            evidence = paymentGateway.VerifyWebhook(payload, signature);
        }
        catch (StripeException)
        {
            return Results.BadRequest();
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (evidence.EventType == "checkout.session.expired")
        {
            if (string.Equals(evidence.Purpose, "instock_full", StringComparison.Ordinal))
            {
                var expired = await sender.Send(new ExpireInStockCheckoutCommand(evidence), cancellationToken);
                return expired.IsSuccess ? Results.Ok() : Results.BadRequest();
            }
            if (string.Equals(evidence.Purpose, "preorder_deposit", StringComparison.Ordinal))
            {
                var expired = await sender.Send(new ExpirePreOrderCheckoutCommand(evidence), cancellationToken);
                return expired.IsSuccess ? Results.Ok() : Results.BadRequest();
            }
            return Results.BadRequest();
        }

        if (evidence.EventType is not "checkout.session.completed"
            and not "checkout.session.async_payment_succeeded") return Results.Ok();
        if (!evidence.IsPaid)
            return Results.Ok();

        if (string.Equals(evidence.Purpose, "instock_full", StringComparison.Ordinal))
        {
            var result = await sender.Send(new FulfillInStockCheckoutCommand(evidence), cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest();
        }
        if (string.Equals(evidence.Purpose, "preorder_deposit", StringComparison.Ordinal))
        {
            var result = await sender.Send(new FulfillPreOrderCheckoutCommand(evidence), cancellationToken);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest();
        }

        return Results.BadRequest();
    }
}
