using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.FulfillPreOrderCheckout;

public sealed record FulfillPreOrderCheckoutCommand(PaymentWebhookEvidence Evidence)
    : IRequest<Result<FulfilledPreOrderCheckout>>;
