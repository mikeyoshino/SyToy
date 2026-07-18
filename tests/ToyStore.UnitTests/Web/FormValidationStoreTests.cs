using Microsoft.AspNetCore.Components.Forms;
using ToyStore.Application.Common.Models;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web;

public sealed class FormValidationStoreTests
{
    [Fact]
    public void DisplayMapsSimpleAndNestedFieldsAndAggregatesMultipleThaiErrors()
    {
        var model = new CheckoutModel();
        var editContext = new EditContext(model);
        var validationStore = new FormValidationStore(editContext);

        validationStore.Display(new[]
        {
            new FieldValidationFailure(nameof(CheckoutModel.Email), "กรุณากรอกอีเมล"),
            new FieldValidationFailure(nameof(CheckoutModel.Email), "รูปแบบอีเมลไม่ถูกต้อง"),
            new FieldValidationFailure("Address.Postcode", "กรุณากรอกรหัสไปรษณีย์"),
        });

        Assert.Collection(
            editContext.GetValidationMessages(editContext.Field(nameof(CheckoutModel.Email))),
            message => Assert.Equal("กรุณากรอกอีเมล", message),
            message => Assert.Equal("รูปแบบอีเมลไม่ถูกต้อง", message));
        Assert.Collection(
            editContext.GetValidationMessages(new FieldIdentifier(model.Address, nameof(AddressModel.Postcode))),
            message => Assert.Equal("กรุณากรอกรหัสไปรษณีย์", message));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Address.Unknown")]
    [InlineData("")]
    public void DisplayMapsUnknownOrModelLevelFailuresToSummaryWithoutThrowing(string propertyName)
    {
        var model = new CheckoutModel();
        var editContext = new EditContext(model);
        var validationStore = new FormValidationStore(editContext);

        validationStore.Display(new[] { new FieldValidationFailure(propertyName, "ข้อมูลไม่ถูกต้อง") });

        Assert.Collection(
            editContext.GetValidationMessages(new FieldIdentifier(model, string.Empty)),
            message => Assert.Equal("ข้อมูลไม่ถูกต้อง", message));
    }

    [Fact]
    public void DisplayClearsStaleMessagesAndNotifiesExactlyOncePerCall()
    {
        var model = new CheckoutModel();
        var editContext = new EditContext(model);
        var validationStore = new FormValidationStore(editContext);
        var notifications = 0;
        editContext.OnValidationStateChanged += (_, _) => notifications++;

        validationStore.Display(new[] { new FieldValidationFailure(nameof(CheckoutModel.Email), "ข้อความเดิม") });
        validationStore.Display(new[] { new FieldValidationFailure("Address.Postcode", "ข้อความใหม่") });

        Assert.Empty(editContext.GetValidationMessages(editContext.Field(nameof(CheckoutModel.Email))));
        Assert.Collection(
            editContext.GetValidationMessages(new FieldIdentifier(model.Address, nameof(AddressModel.Postcode))),
            message => Assert.Equal("ข้อความใหม่", message));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void ClearRemovesAllMessagesAndNotifies()
    {
        var model = new CheckoutModel();
        var editContext = new EditContext(model);
        var validationStore = new FormValidationStore(editContext);
        validationStore.Display(new[] { new FieldValidationFailure(nameof(CheckoutModel.Email), "กรุณากรอกอีเมล") });
        var notifications = 0;
        editContext.OnValidationStateChanged += (_, _) => notifications++;

        validationStore.Clear();

        Assert.Empty(editContext.GetValidationMessages());
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void EditingOneFieldClearsOnlyThatFieldsServerFailures()
    {
        var model = new CheckoutModel();
        var editContext = new EditContext(model);
        using var validationStore = new FormValidationStore(editContext);
        validationStore.Display(new[]
        {
            new FieldValidationFailure(nameof(CheckoutModel.Email), "อีเมลนี้ถูกใช้งานแล้ว"),
            new FieldValidationFailure("Address.Postcode", "รหัสไปรษณีย์ไม่ถูกต้อง"),
        });

        editContext.NotifyFieldChanged(editContext.Field(nameof(CheckoutModel.Email)));

        Assert.Empty(editContext.GetValidationMessages(editContext.Field(nameof(CheckoutModel.Email))));
        Assert.Collection(
            editContext.GetValidationMessages(new FieldIdentifier(model.Address, nameof(AddressModel.Postcode))),
            message => Assert.Equal("รหัสไปรษณีย์ไม่ถูกต้อง", message));
    }

    private sealed class CheckoutModel
    {
        public string Email { get; set; } = string.Empty;

        public AddressModel Address { get; } = new();
    }

    private sealed class AddressModel
    {
        public string Postcode { get; set; } = string.Empty;
    }
}
