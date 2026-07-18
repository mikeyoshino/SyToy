using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory.GetInventoryAvailability;

public sealed class GetInventoryAvailabilityHandler(
    IInventoryReadStore readStore,
    TimeProvider timeProvider)
    : IRequestHandler<
        GetInventoryAvailabilityQuery,
        Result<InventoryAvailabilityResult>>
{
    public async Task<Result<InventoryAvailabilityResult>> Handle(
        GetInventoryAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var read = await readStore.ReadAvailabilityAsync(
            request.InventoryItemId,
            request.ProductId,
            timeProvider.GetUtcNow().ToUniversalTime(),
            cancellationToken);
        if (read is null)
        {
            return Result<InventoryAvailabilityResult>.Failure(InventoryErrors.NotFound);
        }

        EnsureConsistent(request, read);
        return Result<InventoryAvailabilityResult>.Success(
            new InventoryAvailabilityResult(
                read.InventoryItemId,
                read.ProductId,
                read.OnHandQuantity,
                read.PersistedHeldQuantity,
                read.OnHandQuantity - read.PersistedHeldQuantity,
                read.EffectiveReservedQuantity,
                read.OnHandQuantity - read.EffectiveReservedQuantity,
                read.Version,
                read.UpdatedAtUtc,
                read.UpdatedBy));
    }

    private static void EnsureConsistent(
        GetInventoryAvailabilityQuery request,
        InventoryAvailabilityReadModel read)
    {
        if (read.InventoryItemId != request.InventoryItemId
            || read.ProductId != request.ProductId
            || read.OnHandQuantity < 0
            || read.PersistedHeldQuantity < 0
            || read.PhysicalActiveReservedQuantity < 0
            || read.EffectiveReservedQuantity < 0
            || read.PersistedHeldQuantity > read.OnHandQuantity
            || read.PhysicalActiveReservedQuantity != read.PersistedHeldQuantity
            || read.EffectiveReservedQuantity > read.PhysicalActiveReservedQuantity
            || read.Version <= 0
            || read.UpdatedAtUtc.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(read.UpdatedBy))
        {
            throw new InvalidOperationException(
                "Persisted Inventory availability evidence is inconsistent.");
        }
    }
}
