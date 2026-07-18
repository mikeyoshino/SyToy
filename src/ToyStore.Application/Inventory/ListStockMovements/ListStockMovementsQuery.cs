using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory.ListStockMovements;

public sealed record ListStockMovementsQuery(
    Guid InventoryItemId,
    Guid ProductId,
    int Page = 1,
    int PageSize = 20)
    : AuthorizedResultRequest<Result<PagedResult<StockMovementListItem>>>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public override Result<PagedResult<StockMovementListItem>> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PagedResult<StockMovementListItem>>.Failure(
            requestError,
            validationFailures);
}
