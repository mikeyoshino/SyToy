using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.GetAnonymousCartPreview;

public sealed record GetAnonymousCartPreviewQuery(
    IReadOnlyList<AnonymousCartPreviewInput> Items)
    : MediatR.IRequest<Result<CustomerCartView>>;
