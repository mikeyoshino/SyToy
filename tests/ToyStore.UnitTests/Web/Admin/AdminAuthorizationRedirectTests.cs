using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Authorization;
using ToyStore.Web.Components.Account.Shared;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminAuthorizationRedirectTests
{
    [Theory]
    [InlineData(null, false, "/Account/Login?ReturnUrl=%2Fadmin%2Forders%3Ftype%3Dpre-order")]
    [InlineData(RoleNames.Customer, false, "/Account/AccessDenied")]
    [InlineData(RoleNames.Customer, true, "/Account/AccessDenied")]
    [InlineData(RoleNames.Admin, true, "/Account/Manage/ChangePassword")]
    public async Task EnhancedNotAuthorizedSelectsAuthenticationAwareLocalDestination(
        string? role,
        bool mustChangePassword,
        string expectedDestination)
    {
        var principal = CreatePrincipal(role, mustChangePassword);
        var navigation = new RecordingNavigationManager(
            "https://shop.example/admin/orders?type=pre-order");
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(principal));
        services.AddSingleton<NavigationManager>(navigation);

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.RenderComponentAsync<RedirectToAdminAccess>(ParameterView.Empty));

        Assert.Equal(expectedDestination, navigation.Destination);
        Assert.StartsWith("/", navigation.Destination, StringComparison.Ordinal);
        Assert.DoesNotContain("shop.example", navigation.Destination, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal CreatePrincipal(string? role, bool mustChangePassword)
    {
        if (role is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (mustChangePassword)
        {
            claims.Add(new Claim(IdentityClaimNames.MustChangePassword, bool.TrueString));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager(string currentUri)
        {
            Initialize("https://shop.example/", currentUri);
        }

        public string Destination { get; private set; } = string.Empty;

        protected override void NavigateToCore(string uri, bool forceLoad) => Destination = uri;

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Destination = uri;
    }
}
