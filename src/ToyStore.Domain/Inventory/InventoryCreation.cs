namespace ToyStore.Domain.Inventory;

public sealed class InventoryCreation
{
    private InventoryCreation(InventoryItem item, StockMovement initialMovement)
    {
        Item = item;
        InitialMovement = initialMovement;
    }

    public InventoryItem Item { get; }

    public StockMovement InitialMovement { get; }

    internal static InventoryCreation Create(
        InventoryItem item,
        StockMovement initialMovement) =>
        new(item, initialMovement);
}
