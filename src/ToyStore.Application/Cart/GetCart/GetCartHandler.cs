using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.GetCart;

public sealed class GetCartHandler(ICartReader reader)
    : IRequestHandler<GetCartQuery, Result<CustomerCartView>>
{
    public async Task<Result<CustomerCartView>> Handle(
        GetCartQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Cart query reached its handler without an authorized customer.");
        return Result<CustomerCartView>.Success(
            await reader.GetAsync(customerId, cancellationToken));
    }
}
