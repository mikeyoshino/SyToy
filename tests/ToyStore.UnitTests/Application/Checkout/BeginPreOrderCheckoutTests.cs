using ToyStore.Application.Checkout.BeginPreOrderCheckout;
using ToyStore.Application.Common.Authorization;

namespace ToyStore.UnitTests.Application.Checkout;

public sealed class BeginPreOrderCheckoutTests
{
    [Fact]
    public async Task CommandIsCustomerOnlyAndValidatorReturnsThaiFieldFailures()
    {
        var command = new BeginPreOrderCheckoutCommand(Guid.Empty, 0, Guid.Empty,
            "", "abc", "", "", "", "", "12");

        Assert.Equal(PolicyNames.CanUseCustomerCart, command.RequiredPolicy);
        var validation = await new BeginPreOrderCheckoutValidator().ValidateAsync(
            command, TestContext.Current.CancellationToken);

        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(command.ProductId));
        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(command.Quantity));
        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(command.ClientRequestId));
        Assert.Contains(validation.Errors, x => x.PropertyName == nameof(command.PostalCode)
            && x.ErrorMessage == "รหัสไปรษณีย์ต้องเป็นตัวเลข 5 หลัก");
    }
}
