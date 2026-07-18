using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;

public sealed record GetPreOrderCheckoutEligibilityQuery(Guid ProductId, int Quantity)
    : AuthorizedResultRequest<Result<PreOrderCheckoutEligibilityResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;

    public override Result<PreOrderCheckoutEligibilityResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PreOrderCheckoutEligibilityResult>.Failure(requestError, validationFailures);
}
