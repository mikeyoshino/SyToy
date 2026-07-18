using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory.GetInventoryAvailability;

public sealed record GetInventoryAvailabilityQuery(
    Guid InventoryItemId,
    Guid ProductId)
    : AuthorizedResultRequest<Result<InventoryAvailabilityResult>>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public override Result<InventoryAvailabilityResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<InventoryAvailabilityResult>.Failure(requestError, validationFailures);
}
