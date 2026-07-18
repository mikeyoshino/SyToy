using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory.ListStockMovements;

public sealed class ListStockMovementsHandler(IInventoryReadStore readStore)
    : IRequestHandler<
        ListStockMovementsQuery,
        Result<PagedResult<StockMovementListItem>>>
{
    public async Task<Result<PagedResult<StockMovementListItem>>> Handle(
        ListStockMovementsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = await readStore.ReadMovementsAsync(
            new StockMovementReadRequest(
                request.InventoryItemId,
                request.ProductId,
                request.Page,
                request.PageSize),
            cancellationToken);
        if (page is null)
        {
            return Result<PagedResult<StockMovementListItem>>.Failure(
                InventoryErrors.NotFound);
        }

        var items = page.Items.Select(item => new StockMovementListItem(
            item.Id,
            item.InventoryItemId,
            item.ProductId,
            item.Type,
            item.QuantityDelta,
            item.ResultingOnHandQuantity,
            item.ResultingInventoryVersion,
            item.Reason,
            item.Reference,
            item.Actor,
            item.ReservationId,
            item.OccurredAtUtc)).ToArray();
        return Result<PagedResult<StockMovementListItem>>.Success(
            new PagedResult<StockMovementListItem>(
                items,
                page.EffectivePageNumber,
                request.PageSize,
                page.TotalCount));
    }
}
