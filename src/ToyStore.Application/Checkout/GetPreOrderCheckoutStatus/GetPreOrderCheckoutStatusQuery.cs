using MediatR;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Checkout.GetPreOrderCheckoutStatus;

public sealed record GetPreOrderCheckoutStatusQuery(Guid CheckoutAttemptId)
    : AuthorizedResultRequest<Result<PreOrderCheckoutStatusResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;
    public override Result<PreOrderCheckoutStatusResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PreOrderCheckoutStatusResult>.Failure(requestError, validationFailures);
}

public sealed class GetPreOrderCheckoutStatusHandler(IPreOrderCheckoutStore repository)
    : IRequestHandler<GetPreOrderCheckoutStatusQuery, Result<PreOrderCheckoutStatusResult>>
{
    public Task<Result<PreOrderCheckoutStatusResult>> Handle(
        GetPreOrderCheckoutStatusQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Checkout status requires an authorized customer.");
        return repository.GetStatusAsync(customerId, request.CheckoutAttemptId, cancellationToken);
    }
}
