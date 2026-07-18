using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory;

public sealed record InventoryMutationResult(
    Guid InventoryItemId,
    Guid ProductId,
    int OnHandQuantity,
    int HeldQuantity,
    int ReservableQuantity,
    long Version,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy,
    bool Changed)
{
    public static InventoryMutationResult From(InventoryItem item, bool changed)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new InventoryMutationResult(
            item.Id,
            item.ProductId,
            item.OnHandQuantity,
            item.HeldQuantity,
            item.ReservableQuantity,
            item.Version,
            item.UpdatedAtUtc,
            item.UpdatedBy,
            changed);
    }

    public static InventoryMutationResult From(
        InventoryMutationEvidence evidence,
        bool changed)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new InventoryMutationResult(
            evidence.InventoryItemId,
            evidence.ProductId,
            evidence.IntendedOnHandQuantity,
            evidence.IntendedHeldQuantity,
            evidence.IntendedOnHandQuantity - evidence.IntendedHeldQuantity,
            evidence.IntendedVersion,
            evidence.IntendedUpdatedAtUtc,
            evidence.IntendedUpdatedBy,
            changed);
    }
}
