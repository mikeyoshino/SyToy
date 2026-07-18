using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.GetAnonymousCartPreview;

public sealed class GetAnonymousCartPreviewHandler(ICartReader reader)
    : IRequestHandler<GetAnonymousCartPreviewQuery, Result<CustomerCartView>>
{
    public async Task<Result<CustomerCartView>> Handle(
        GetAnonymousCartPreviewQuery request,
        CancellationToken cancellationToken) =>
        Result<CustomerCartView>.Success(await reader.PreviewAsync(
            request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => new AnonymousCartPreviewInput(
                    group.Key,
                    (int)Math.Min(group.Sum(item => (long)item.Quantity), ToyStore.Domain.Carts.CartLimits.MaximumQuantityPerItem)))
                .OrderBy(item => item.ProductId)
                .ToArray(),
            cancellationToken));
}
