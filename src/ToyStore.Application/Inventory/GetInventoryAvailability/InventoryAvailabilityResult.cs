namespace ToyStore.Application.Inventory.GetInventoryAvailability;

public sealed record InventoryAvailabilityResult(
    Guid InventoryItemId,
    Guid ProductId,
    int OnHandQuantity,
    int PhysicalHeldQuantity,
    int ReservableQuantity,
    int EffectiveReservedQuantity,
    int CustomerAvailableQuantity,
    long Version,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy);
