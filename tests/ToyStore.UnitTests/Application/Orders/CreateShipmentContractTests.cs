using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Orders.CreateShipment;

namespace ToyStore.UnitTests.Application.Orders;

public sealed class CreateShipmentContractTests
{
    [Fact]
    public void CommandRequiresOrderPolicyAndValidatorReturnsThaiFailures()
    {
        var command = new CreateShipmentCommand("", AdminShippingCarrier.Other, "TRACK-12345",
            "http://unsafe.test", 0, Guid.Empty);
        var failures = new CreateShipmentValidator().Validate(command).Errors;
        Assert.Equal(PolicyNames.CanManageOrders, command.RequiredPolicy);
        Assert.Contains(failures, x => x.ErrorMessage.Contains("เลขคำสั่งซื้อ", StringComparison.Ordinal));
        Assert.Contains(failures, x => x.ErrorMessage.Contains("HTTPS", StringComparison.Ordinal));
        Assert.Contains(failures, x => x.ErrorMessage.Contains("เวอร์ชัน", StringComparison.Ordinal));
    }
}
