using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.FulfillInStockCheckout;

public sealed record FulfillInStockCheckoutCommand(PaymentWebhookEvidence Evidence)
    : IRequest<Result<FulfilledInStockCheckout>>;

public sealed class FulfillInStockCheckoutHandler(IInStockCheckoutStore checkoutStore)
    : IRequestHandler<FulfillInStockCheckoutCommand, Result<FulfilledInStockCheckout>>
{
    public Task<Result<FulfilledInStockCheckout>> Handle(
        FulfillInStockCheckoutCommand request,
        CancellationToken cancellationToken) =>
        checkoutStore.FulfillAsync(request.Evidence, cancellationToken);
}
