using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Web.Identity;

namespace ToyStore.UnitTests.Web;

public sealed class CurrentUserAuthorizationTests
{
    [Fact]
    public async Task UsesCircuitAuthenticationStateAndAspNetPolicyForActor()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "admin-42"),
            new Claim(ClaimTypes.Role, RoleNames.Admin));
        using var provider = CreateProvider(principal);
        var authorization = new CurrentUserAuthorization(
            provider.GetRequiredService<AuthenticationStateProvider>(),
            provider.GetRequiredService<IAuthorizationService>());

        var result = await authorization.AuthorizeAsync(
            PolicyNames.CanManageProducts,
            CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.True(result.IsAuthorized);
        Assert.Equal("admin-42", result.ActorId);
    }

    [Fact]
    public async Task AnonymousCircuitReturnsUnauthenticatedWithoutActor()
    {
        using var provider = CreateProvider(new ClaimsPrincipal(new ClaimsIdentity()));
        var authorization = new CurrentUserAuthorization(
            provider.GetRequiredService<AuthenticationStateProvider>(),
            provider.GetRequiredService<IAuthorizationService>());

        var result = await authorization.AuthorizeAsync(
            PolicyNames.CanManageProducts,
            CancellationToken.None);

        Assert.False(result.IsAuthenticated);
        Assert.False(result.IsAuthorized);
        Assert.Null(result.ActorId);
    }

    private static ServiceProvider CreateProvider(ClaimsPrincipal principal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder()
            .AddPolicy(
                PolicyNames.CanManageProducts,
                policy => policy.RequireRole(RoleNames.Admin));
        services.AddSingleton<AuthenticationStateProvider>(
            new FixedAuthenticationStateProvider(principal));
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }
}
