using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.CreateShipment;

public enum AdminShippingCarrier { ThailandPost, Flash, Kerry, JAndT, Other }

public sealed record CreateShipmentCommand(string OrderNumber, AdminShippingCarrier Carrier,
    string TrackingNumber, string? OtherTrackingUrl, long ExpectedOrderVersion, Guid OperationId)
    : AuthorizedResultRequest<Result<CreateShipmentResult>>
{
    public override string RequiredPolicy => PolicyNames.CanManageOrders;
    public override Result<CreateShipmentResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CreateShipmentResult>.Failure(requestError, validationFailures);
}
