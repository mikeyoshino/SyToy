using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.FulfillPreOrderCheckout;

public sealed class FulfillPreOrderCheckoutHandler(IPreOrderCheckoutStore repository)
    : IRequestHandler<FulfillPreOrderCheckoutCommand, Result<FulfilledPreOrderCheckout>>
{
    public Task<Result<FulfilledPreOrderCheckout>> Handle(
        FulfillPreOrderCheckoutCommand request,
        CancellationToken cancellationToken) => repository.FulfillAsync(request.Evidence, cancellationToken);
}
