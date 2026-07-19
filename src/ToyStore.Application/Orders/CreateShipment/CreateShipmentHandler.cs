using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Orders;

namespace ToyStore.Application.Orders.CreateShipment;

public sealed class CreateShipmentHandler(IShipmentMutationStore store, TimeProvider timeProvider)
    : IRequestHandler<CreateShipmentCommand, Result<CreateShipmentResult>>
{
    public Task<Result<CreateShipmentResult>> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var actor = request.AuthorizedActorId ?? throw new InvalidOperationException("Shipment command requires Admin authorization.");
        return store.CreateAsync(new ShipmentMutationRequest(request.OrderNumber.Trim(),
            (ShippingCarrier)request.Carrier, request.TrackingNumber.Trim(), request.OtherTrackingUrl?.Trim(),
            request.ExpectedOrderVersion, request.OperationId, actor,
            timeProvider.GetUtcNow().ToUniversalTime()), cancellationToken);
    }
}
