using ToyStore.Application.Common.Models;
using ToyStore.Domain.Orders;

namespace ToyStore.Application.Orders;

public interface IShipmentMutationStore
{
    Task<Result<CreateShipmentResult>> CreateAsync(ShipmentMutationRequest request, CancellationToken cancellationToken);
}

public sealed record ShipmentMutationRequest(string OrderNumber, ShippingCarrier Carrier,
    string TrackingNumber, string? OtherTrackingUrl, long ExpectedVersion, Guid OperationId,
    string ActorId, DateTimeOffset NowUtc);

public sealed record CreateShipmentResult(string OrderNumber, string Carrier, string TrackingNumber,
    string TrackingUrl, DateTimeOffset ShippedAtUtc, long OrderVersion, bool Changed);
