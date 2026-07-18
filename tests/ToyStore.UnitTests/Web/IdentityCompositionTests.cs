using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Web.Identity;

namespace ToyStore.UnitTests.Web;

public sealed class IdentityCompositionTests
{
    [Fact]
    public async Task ManagementPoliciesRequireAdminWithoutForceChangeClaim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddToyStoreWebIdentity(
            CreateConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            new FakeEnvironment("Testing"));

        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var admin = CreatePrincipal(RoleNames.Admin);
        var forcedAdmin = CreatePrincipal(
            RoleNames.Admin,
            new Claim(IdentityClaimNames.MustChangePassword, bool.TrueString));
        var customer = CreatePrincipal(RoleNames.Customer);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        foreach (var policyName in ManagementPolicies())
        {
            Assert.True((await authorization.AuthorizeAsync(admin, null, policyName)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(forcedAdmin, null, policyName)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(customer, null, policyName)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(anonymous, null, policyName)).Succeeded);
        }
    }

    [Fact]
    public async Task CustomerCartPolicyRequiresCustomerRole()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddToyStoreWebIdentity(
            CreateConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            new FakeEnvironment("Testing"));

        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(
            CreatePrincipal(RoleNames.Customer), null, PolicyNames.CanUseCustomerCart)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            CreatePrincipal(RoleNames.Admin), null, PolicyNames.CanUseCustomerCart)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), null, PolicyNames.CanUseCustomerCart)).Succeeded);
    }

    [Fact]
    public void UserContextResolvesFromTheWebLayer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddToyStoreWebIdentity(
            CreateConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            new FakeEnvironment("Testing"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<HttpUserContext>(scope.ServiceProvider.GetRequiredService<IUserContext>());
    }

    [Fact]
    public void CircuitCurrentUserAuthorizationResolvesFromTheWebLayer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
            _ => new FixedAuthenticationStateProvider());
        services.AddToyStoreWebIdentity(
            CreateConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            new FakeEnvironment("Testing"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<CurrentUserAuthorization>(
            scope.ServiceProvider.GetRequiredService<ICurrentUserAuthorization>());
    }

    [Fact]
    public void ProductionRejectsMissingDataProtectionPath()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddToyStoreWebIdentity(configuration, new FakeEnvironment("Production")));

        Assert.Equal("DataProtection:KeysPath is required outside Development.", exception.Message);
    }

    [Fact]
    public void AccountSurfaceIsThaiFirstAndContainsNoUnsupportedLoginMethods()
    {
        var repositoryRoot = FindRepositoryRoot();
        var app = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "ToyStore.Web", "Components", "App.razor"));
        var accountRoot = Path.Combine(
            repositoryRoot,
            "src",
            "ToyStore.Web",
            "Components",
            "Account");
        var accountText = string.Join(
            Environment.NewLine,
            Directory.GetFiles(accountRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                    || path.EndsWith(".cs", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.Contains("<html lang=\"th\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("PasskeySubmit", app, StringComparison.Ordinal);
        Assert.Contains("เข้าสู่ระบบ", accountText, StringComparison.Ordinal);
        Assert.Contains("สมัครสมาชิก", accountText, StringComparison.Ordinal);
        Assert.DoesNotContain("PerformExternalLogin", accountText, StringComparison.Ordinal);
        Assert.DoesNotContain("PasskeyCreationOptions", accountText, StringComparison.Ordinal);
        Assert.DoesNotContain("Log in with a passkey", accountText, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatorBackedAccountFormsMapStructuredApplicationFailuresToFields()
    {
        var repositoryRoot = FindRepositoryRoot();
        var accountPages = new[]
        {
            Path.Combine(
                repositoryRoot,
                "src",
                "ToyStore.Web",
                "Components",
                "Account",
                "Pages",
                "Register.razor"),
            Path.Combine(
                repositoryRoot,
                "src",
                "ToyStore.Web",
                "Components",
                "Account",
                "Pages",
                "Manage",
                "ChangePassword.razor"),
        };

        foreach (var path in accountPages)
        {
            var source = File.ReadAllText(path);
            Assert.Contains("EditContext=\"editContext\"", source, StringComparison.Ordinal);
            Assert.Contains("new FormValidationStore(editContext)", source, StringComparison.Ordinal);
            Assert.Contains("ValidationFailures", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WebAccountComponentsDoNotDependOnInfrastructureIdentityImplementations()
    {
        var repositoryRoot = FindRepositoryRoot();
        var accountRoot = Path.Combine(
            repositoryRoot,
            "src",
            "ToyStore.Web",
            "Components",
            "Account");
        var accountSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(accountRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                    || path.EndsWith(".cs", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            "ToyStore.Infrastructure.Identity",
            accountSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UserManager<", accountSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationUser", accountSource, StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(string keysPath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = keysPath,
            })
            .Build();

    private static ClaimsPrincipal CreatePrincipal(string role, Claim? extraClaim = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (extraClaim is not null)
        {
            claims.Add(extraClaim);
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static string[] ManagementPolicies() =>
    [
        "CanAccessAdmin",
        PolicyNames.CanManageProducts,
        PolicyNames.CanManageOrders,
        PolicyNames.CanVerifyPayments,
        PolicyNames.CanManageUsers,
    ];

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private sealed class FakeEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ToyStore";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = environmentName;

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedAuthenticationStateProvider
        : Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider
    {
        public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>
            GetAuthenticationStateAsync() =>
            Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
