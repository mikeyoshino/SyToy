using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.GetCart;

public sealed record GetCartQuery : AuthorizedCartRequest<Result<CustomerCartView>>
{
    public override Result<CustomerCartView> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CustomerCartView>.Failure(requestError, validationFailures);
}
