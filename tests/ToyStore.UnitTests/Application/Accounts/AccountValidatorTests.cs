using ToyStore.Application.Accounts.ChangePassword;
using ToyStore.Application.Accounts.RegisterCustomer;

namespace ToyStore.UnitTests.Application.Accounts;

public sealed class AccountValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void RegisterRejectsMissingOrInvalidEmail(string email)
    {
        var result = new RegisterCustomerValidator().Validate(
            new RegisterCustomerCommand(email, "Password1", "Password1"));

        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    [Theory]
    [InlineData("Short1")]
    [InlineData("password1")]
    [InlineData("PASSWORD1")]
    [InlineData("Password")]
    public void RegisterRejectsPasswordThatDoesNotMeetPolicy(string password)
    {
        var result = new RegisterCustomerValidator().Validate(
            new RegisterCustomerCommand("customer@example.com", password, password));

        Assert.Contains(result.Errors, error => error.PropertyName == "Password");
    }

    [Fact]
    public void RegisterRejectsMismatchedConfirmation()
    {
        var result = new RegisterCustomerValidator().Validate(
            new RegisterCustomerCommand(
                "customer@example.com",
                "Password1",
                "Different1"));

        var failure = Assert.Single(
            result.Errors,
            error => error.PropertyName == "ConfirmPassword");
        Assert.Equal("รหัสผ่านและการยืนยันรหัสผ่านไม่ตรงกัน", failure.ErrorMessage);
    }

    [Fact]
    public void ChangePasswordAcceptsValidThaiFirstInputContract()
    {
        var result = new ChangePasswordValidator().Validate(
            new ChangePasswordCommand("Current1", "NewPassword1", "NewPassword1"));

        Assert.True(result.IsValid);
    }
}
