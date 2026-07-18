using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Inventory;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class InventoryReadStore(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IInventoryReadStore
{
    public async Task<InventoryAvailabilityReadModel?> ReadAvailabilityAsync(
        Guid inventoryItemId,
        Guid productId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        return await dbContext.InventoryItems
            .AsNoTracking()
            .Where(item => item.Id == inventoryItemId && item.ProductId == productId)
            .Select(item => new InventoryAvailabilityReadModel(
                item.Id,
                item.ProductId,
                item.OnHandQuantity,
                item.HeldQuantity,
                dbContext.StockReservations
                    .Where(reservation =>
                        reservation.InventoryItemId == item.Id
                        && reservation.ProductId == item.ProductId
                        && reservation.Status == StockReservationStatus.Active)
                    .Sum(reservation => (int?)reservation.Quantity) ?? 0,
                dbContext.StockReservations
                    .Where(reservation =>
                        reservation.InventoryItemId == item.Id
                        && reservation.ProductId == item.ProductId
                        && reservation.Status == StockReservationStatus.Active
                        && reservation.ExpiresAtUtc > nowUtc)
                    .Sum(reservation => (int?)reservation.Quantity) ?? 0,
                item.Version,
                item.UpdatedAtUtc,
                item.UpdatedBy))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<StockMovementReadPage?> ReadMovementsAsync(
        StockMovementReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.PageSize, 100);

        await using var dbContext = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var ownsInventory = await dbContext.InventoryItems
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == request.InventoryItemId
                    && item.ProductId == request.ProductId,
                cancellationToken);
        if (!ownsInventory)
        {
            return null;
        }

        var query = dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.InventoryItemId == request.InventoryItemId
                && movement.ProductId == request.ProductId);
        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePage = EffectivePageNumber(
            request.PageNumber,
            request.PageSize,
            totalCount);
        var items = totalCount == 0
            ? []
            : await query
                .OrderByDescending(movement => movement.OccurredAtUtc)
                .ThenByDescending(movement => movement.Id)
                .Skip((effectivePage - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(movement => new StockMovementReadModel(
                    movement.Id,
                    movement.InventoryItemId,
                    movement.ProductId,
                    movement.Type,
                    movement.QuantityDelta,
                    movement.ResultingOnHandQuantity,
                    movement.ResultingInventoryVersion,
                    movement.Reason,
                    movement.Reference,
                    movement.Actor,
                    movement.ReservationId,
                    movement.OccurredAtUtc))
                .ToArrayAsync(cancellationToken);
        return new StockMovementReadPage(items, effectivePage, totalCount);
    }

    private static int EffectivePageNumber(
        int requestedPage,
        int pageSize,
        int totalCount)
    {
        if (totalCount == 0)
        {
            return 1;
        }

        var lastPage = (int)(((long)totalCount + pageSize - 1) / pageSize);
        return Math.Min(requestedPage, lastPage);
    }
}
