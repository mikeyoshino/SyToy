using MediatR;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.GetInStockCheckoutStatus;

public sealed record GetInStockCheckoutStatusQuery(Guid CheckoutAttemptId)
    : AuthorizedResultRequest<Result<InStockCheckoutStatusResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;
    public override Result<InStockCheckoutStatusResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<InStockCheckoutStatusResult>.Failure(requestError, validationFailures);
}

public sealed class GetInStockCheckoutStatusHandler(IInStockCheckoutStore checkoutStore)
    : IRequestHandler<GetInStockCheckoutStatusQuery, Result<InStockCheckoutStatusResult>>
{
    public Task<Result<InStockCheckoutStatusResult>> Handle(
        GetInStockCheckoutStatusQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Checkout status requires an authorized customer.");
        return checkoutStore.GetStatusAsync(customerId, request.CheckoutAttemptId, cancellationToken);
    }
}
