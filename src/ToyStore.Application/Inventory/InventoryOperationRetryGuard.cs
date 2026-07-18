using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory;

public static class InventoryOperationRetryGuard
{
    public static void EnsureOwningState(
        InventoryItem item,
        StockMovement movement)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(movement);
        if (item.Id != movement.InventoryItemId || item.ProductId != movement.ProductId)
        {
            throw new InvalidOperationException(
                "Exact Inventory retry evidence does not belong to the locked Inventory row.");
        }

        if (item.Version < movement.ResultingInventoryVersion)
        {
            throw new InvalidOperationException(
                "Exact Inventory retry movement is ahead of its owning Inventory row.");
        }

        if (item.Version > movement.ResultingInventoryVersion)
        {
            return;
        }

        if (item.OnHandQuantity != movement.ResultingOnHandQuantity
            || NormalizePostgresInstant(item.UpdatedAtUtc)
                != NormalizePostgresInstant(movement.OccurredAtUtc)
            || item.UpdatedBy != movement.Actor)
        {
            throw new InvalidOperationException(
                "Exact Inventory retry owning state is inconsistent with its movement.");
        }
    }

    private static DateTimeOffset NormalizePostgresInstant(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}
