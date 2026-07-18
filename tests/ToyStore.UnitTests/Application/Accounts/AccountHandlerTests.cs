using ToyStore.Application.Accounts;
using ToyStore.Application.Accounts.ChangePassword;
using ToyStore.Application.Accounts.Login;
using ToyStore.Application.Accounts.Logout;
using ToyStore.Application.Accounts.RegisterCustomer;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;

namespace ToyStore.UnitTests.Application.Accounts;

public sealed class AccountHandlerTests
{
    [Fact]
    public void RolesContainOnlyCustomerAndAdmin()
    {
        Assert.Equal(["Customer", "Admin"], RoleNames.All);
    }

    [Fact]
    public void ManagementPolicyNamesAreStable()
    {
        var adminPolicy = typeof(PolicyNames).GetField("CanAccessAdmin");

        Assert.NotNull(adminPolicy);
        Assert.Equal("CanAccessAdmin", adminPolicy.GetRawConstantValue());
        Assert.Equal("CanManageProducts", PolicyNames.CanManageProducts);
        Assert.Equal("CanManageOrders", PolicyNames.CanManageOrders);
        Assert.Equal("CanVerifyPayments", PolicyNames.CanVerifyPayments);
        Assert.Equal("CanManageUsers", PolicyNames.CanManageUsers);
    }

    [Fact]
    public async Task RegisterDelegatesToIdentityAndForwardsCancellation()
    {
        var identity = new FakeIdentityService
        {
            RegisterResult = Result<string>.Success("user-123"),
        };
        var handler = new RegisterCustomerHandler(identity);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await handler.Handle(
            new RegisterCustomerCommand(
                "customer@example.com",
                "Password1",
                "Password1"),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-123", result.Value);
        Assert.Equal("customer@example.com", identity.Email);
        Assert.Equal("Password1", identity.Password);
        Assert.Equal(cancellationToken, identity.CancellationToken);
    }

    [Fact]
    public async Task LoginPreservesExpectedFailureWithoutThrowing()
    {
        var identity = new FakeIdentityService
        {
            LoginResult = Result<LoginResult>.Failure(AccountErrors.LockedOut),
        };
        var handler = new LoginHandler(identity);

        var result = await handler.Handle(
            new LoginCommand("customer@example.com", "WrongPassword1", true),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.LockedOut, result.Error);
        Assert.True(identity.RememberMe);
    }

    [Fact]
    public async Task ChangePasswordRejectsMissingCurrentUser()
    {
        var identity = new FakeIdentityService();
        var handler = new ChangePasswordHandler(identity, new FakeUserContext(null));

        var result = await handler.Handle(
            new ChangePasswordCommand("Current1", "NewPassword1", "NewPassword1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.NotAuthenticated, result.Error);
        Assert.False(identity.ChangePasswordCalled);
    }

    [Fact]
    public async Task LogoutDelegatesToIdentity()
    {
        var identity = new FakeIdentityService();
        var handler = new LogoutHandler(identity);

        var result = await handler.Handle(
            new LogoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(identity.SignOutCalled);
    }

    private sealed class FakeUserContext(string? userId) : IUserContext
    {
        public string? UserId { get; } = userId;
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public Result<string> RegisterResult { get; set; } =
            Result<string>.Failure(AccountErrors.EmailAlreadyUsed);

        public Result<LoginResult> LoginResult { get; set; } =
            Result<LoginResult>.Failure(AccountErrors.InvalidCredentials);

        public string? Email { get; private set; }

        public string? Password { get; private set; }

        public bool RememberMe { get; private set; }

        public bool ChangePasswordCalled { get; private set; }

        public bool SignOutCalled { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Result<string>> RegisterCustomerAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            Email = email;
            Password = password;
            CancellationToken = cancellationToken;
            return Task.FromResult(RegisterResult);
        }

        public Task<Result<LoginResult>> PasswordSignInAsync(
            string email,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken)
        {
            Email = email;
            Password = password;
            RememberMe = rememberMe;
            CancellationToken = cancellationToken;
            return Task.FromResult(LoginResult);
        }

        public Task<Result> ChangePasswordAsync(
            string userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken)
        {
            ChangePasswordCalled = true;
            CancellationToken = cancellationToken;
            return Task.FromResult(Result.Success());
        }

        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            SignOutCalled = true;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
