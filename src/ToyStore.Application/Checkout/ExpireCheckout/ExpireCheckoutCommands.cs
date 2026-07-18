using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.ExpireCheckout;

public sealed record ExpireInStockCheckoutCommand(PaymentWebhookEvidence Evidence)
    : IRequest<Result<ExpiredCheckoutResult>>;

public sealed class ExpireInStockCheckoutHandler(IInStockCheckoutStore checkoutStore)
    : IRequestHandler<ExpireInStockCheckoutCommand, Result<ExpiredCheckoutResult>>
{
    public Task<Result<ExpiredCheckoutResult>> Handle(
        ExpireInStockCheckoutCommand request,
        CancellationToken cancellationToken) => checkoutStore.ExpireAsync(request.Evidence, cancellationToken);
}

public sealed record ExpirePreOrderCheckoutCommand(PaymentWebhookEvidence Evidence)
    : IRequest<Result<ExpiredCheckoutResult>>;

public sealed class ExpirePreOrderCheckoutHandler(IPreOrderCheckoutStore checkoutStore)
    : IRequestHandler<ExpirePreOrderCheckoutCommand, Result<ExpiredCheckoutResult>>
{
    public Task<Result<ExpiredCheckoutResult>> Handle(
        ExpirePreOrderCheckoutCommand request,
        CancellationToken cancellationToken) => checkoutStore.ExpireAsync(request.Evidence, cancellationToken);
}
