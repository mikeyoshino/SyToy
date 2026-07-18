using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Accounts;
using ToyStore.Application.Accounts.BootstrapAdmin;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Identity;

public sealed class IdentityServiceTests
{
    [Fact]
    public async Task RegistrationCreatesCustomerAndRejectsNormalizedDuplicate()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await identity.RegisterCustomerAsync(
            "customer@example.com",
            "Password1",
            TestContext.Current.CancellationToken);
        var duplicate = await identity.RegisterCustomerAsync(
            "CUSTOMER@example.com",
            "Password1",
            TestContext.Current.CancellationToken);

        Assert.True(created.IsSuccess);
        var user = await userManager.FindByIdAsync(created.Value);
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Customer));
        Assert.True(duplicate.IsFailure);
        Assert.Equal(AccountErrors.EmailAlreadyUsed, duplicate.Error);
        Assert.Single(userManager.Users);
    }

    [Fact]
    public async Task FailedPasswordPolicyLeavesNoPartialCustomer()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await identity.RegisterCustomerAsync(
            "customer@example.com",
            "weak",
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountErrors.RegistrationFailed, result.Error);
        Assert.Empty(userManager.Users);
    }

    [Fact]
    public async Task FirstAdminBootstrapIsIdempotentAndRequiresPasswordChange()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await bootstrapper.CreateFirstAdminAsync(
            "admin@example.com",
            "Temporary1",
            TestContext.Current.CancellationToken);
        var repeated = await bootstrapper.CreateFirstAdminAsync(
            "other-admin@example.com",
            "Temporary1",
            TestContext.Current.CancellationToken);

        Assert.True(created.IsSuccess);
        var admin = await userManager.FindByIdAsync(created.Value.UserId);
        Assert.NotNull(admin);
        Assert.True(admin.MustChangePassword);
        Assert.True(await userManager.IsInRoleAsync(admin, RoleNames.Admin));
        Assert.True(repeated.IsFailure);
        Assert.Equal(AccountErrors.AdminAlreadyExists, repeated.Error);
        Assert.Single(await userManager.GetUsersInRoleAsync(RoleNames.Admin));
    }

    [Fact]
    public async Task ConcurrentFirstAdminBootstrapCreatesExactlyOneAdmin()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;

        async Task<Result<BootstrapAdminResult>> BootstrapAsync(
            string email)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await bootstrapper.CreateFirstAdminAsync(
                email,
                "Temporary1",
                TestContext.Current.CancellationToken);
        }

        var results = await Task.WhenAll(
            BootstrapAsync("admin-one@example.com"),
            BootstrapAsync("admin-two@example.com"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.Error == AccountErrors.AdminAlreadyExists);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Single(await userManager.GetUsersInRoleAsync(RoleNames.Admin));
    }

    [Fact]
    public async Task ExistingCustomerEmailCannotBePromotedByFirstAdminBootstrap()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var customerResult = await identity.RegisterCustomerAsync(
            "customer@example.com",
            "Password1",
            TestContext.Current.CancellationToken);
        Assert.True(customerResult.IsSuccess);

        var bootstrapResult = await bootstrapper.CreateFirstAdminAsync(
            "CUSTOMER@example.com",
            "Temporary1",
            TestContext.Current.CancellationToken);

        Assert.True(bootstrapResult.IsFailure);
        Assert.Equal(AccountErrors.AdminBootstrapFailed, bootstrapResult.Error);
        var customer = await userManager.FindByIdAsync(customerResult.Value);
        Assert.NotNull(customer);
        Assert.True(await userManager.IsInRoleAsync(customer, RoleNames.Customer));
        Assert.False(await userManager.IsInRoleAsync(customer, RoleNames.Admin));
        Assert.Empty(await userManager.GetUsersInRoleAsync(RoleNames.Admin));
    }

    [Fact]
    public async Task MissingCustomerRoleRollsBackTheNewUser()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var roleManager = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = await roleManager.FindByNameAsync(RoleNames.Customer);
            Assert.NotNull(role);
            var deleted = await roleManager.DeleteAsync(role);
            Assert.True(deleted.Succeeded);
        }

        await using (var actionScope = factory.Services.CreateAsyncScope())
        {
            var identity = actionScope.ServiceProvider.GetRequiredService<IIdentityService>();
            await Assert.ThrowsAnyAsync<Exception>(() => identity.RegisterCustomerAsync(
                "rollback@example.com",
                "Password1",
                TestContext.Current.CancellationToken));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == "ROLLBACK@EXAMPLE.COM",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingAdminRoleRollsBackTheNewUser()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var roleManager = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var role = await roleManager.FindByNameAsync(RoleNames.Admin);
            Assert.NotNull(role);
            var deleted = await roleManager.DeleteAsync(role);
            Assert.True(deleted.Succeeded);
        }

        await using (var actionScope = factory.Services.CreateAsyncScope())
        {
            var bootstrapper = actionScope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
            await Assert.ThrowsAnyAsync<Exception>(() => bootstrapper.CreateFirstAdminAsync(
                "rollback-admin@example.com",
                "Temporary1",
                TestContext.Current.CancellationToken));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == "ROLLBACK-ADMIN@EXAMPLE.COM",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FiveInvalidPasswordsLockTheCustomerForFifteenMinutes()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await identity.RegisterCustomerAsync(
            "customer@example.com",
            "Password1",
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failed = await identity.PasswordSignInAsync(
                "customer@example.com",
                "WrongPassword1",
                rememberMe: false,
                TestContext.Current.CancellationToken);
            Assert.Equal(AccountErrors.InvalidCredentials, failed.Error);
        }

        var locked = await identity.PasswordSignInAsync(
            "customer@example.com",
            "WrongPassword1",
            rememberMe: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(AccountErrors.LockedOut, locked.Error);
        var customer = await userManager.FindByIdAsync(created.Value);
        Assert.NotNull(customer?.LockoutEnd);
        Assert.InRange(
            customer.LockoutEnd.Value,
            DateTimeOffset.UtcNow.AddMinutes(14),
            DateTimeOffset.UtcNow.AddMinutes(16));
    }
}
