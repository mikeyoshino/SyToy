using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory.ListStockMovements;

public sealed record StockMovementListItem(
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
