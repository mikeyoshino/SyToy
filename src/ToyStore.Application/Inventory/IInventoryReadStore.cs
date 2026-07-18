using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory;

public interface IInventoryReadStore
{
    Task<InventoryAvailabilityReadModel?> ReadAvailabilityAsync(
        Guid inventoryItemId,
        Guid productId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<StockMovementReadPage?> ReadMovementsAsync(
        StockMovementReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record InventoryAvailabilityReadModel(
    Guid InventoryItemId,
    Guid ProductId,
    int OnHandQuantity,
    int PersistedHeldQuantity,
    int PhysicalActiveReservedQuantity,
    int EffectiveReservedQuantity,
    long Version,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy);

public sealed record StockMovementReadRequest(
    Guid InventoryItemId,
    Guid ProductId,
    int PageNumber,
    int PageSize);

public sealed record StockMovementReadPage(
    IReadOnlyList<StockMovementReadModel> Items,
    int EffectivePageNumber,
    int TotalCount);

public sealed record StockMovementReadModel(
    Guid Id,
    Guid InventoryItemId,
    Guid ProductId,
    StockMovementType Type,
    int QuantityDelta,
    int ResultingOnHandQuantity,
    long ResultingInventoryVersion,
    string Reason,
    string Reference,
    string Actor,
    Guid? ReservationId,
    DateTimeOffset OccurredAtUtc);
