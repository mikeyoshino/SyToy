namespace ToyStore.Domain.Inventory;

public sealed class InventoryAvailability
{
    private InventoryAvailability(int effectiveActiveReservedQuantity, int availableQuantity)
    {
        EffectiveActiveReservedQuantity = effectiveActiveReservedQuantity;
        AvailableQuantity = availableQuantity;
    }

    public int EffectiveActiveReservedQuantity { get; }

    public int AvailableQuantity { get; }

    public static InventoryAvailability Calculate(
        InventoryItem item,
        IReadOnlyCollection<StockReservation> completeReservations,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(completeReservations);
        InventoryEvidence.EnsureUtc(nowUtc);

        var ids = new HashSet<Guid>();
        var effectiveQuantity = 0;
        var physicalHeldQuantity = 0;
        foreach (var reservation in completeReservations)
        {
            if (reservation.InventoryItemId != item.Id)
            {
                throw new InventoryRuleException(InventoryRule.ReservationInventoryMismatch);
            }

            if (reservation.ProductId != item.ProductId)
            {
                throw new InventoryRuleException(InventoryRule.ReservationProductMismatch);
            }

            if (!ids.Add(reservation.Id))
            {
                throw new InventoryRuleException(InventoryRule.ReservationIdentityDuplicate);
            }

            if (reservation.Status == StockReservationStatus.Active)
            {
                try
                {
                    physicalHeldQuantity = checked(physicalHeldQuantity + reservation.Quantity);
                    if (reservation.IsEffectiveActiveAt(nowUtc))
                    {
                        effectiveQuantity = checked(effectiveQuantity + reservation.Quantity);
                    }
                }
                catch (OverflowException)
                {
                    throw new InventoryRuleException(InventoryRule.QuantityOverflow);
                }
            }
        }

        if (physicalHeldQuantity != item.HeldQuantity)
        {
            throw new InventoryRuleException(InventoryRule.AvailabilitySnapshotInvalid);
        }

        var available = item.OnHandQuantity - effectiveQuantity;
        if (available < 0)
        {
            throw new InventoryRuleException(InventoryRule.AvailabilitySnapshotInvalid);
        }

        return new InventoryAvailability(effectiveQuantity, available);
    }
}
