using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using ToyStore.Application.Common.Authorization;

namespace ToyStore.Web.Identity;

public sealed class CurrentUserAuthorization(
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService) : ICurrentUserAuthorization
{
    public async Task<CurrentUserAuthorizationResult> AuthorizeAsync(
        string policyName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        cancellationToken.ThrowIfCancellationRequested();

        var authenticationState = await authenticationStateProvider
            .GetAuthenticationStateAsync()
            .ConfigureAwait(false);
        var principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new CurrentUserAuthorizationResult(
                IsAuthenticated: false,
                IsAuthorized: false,
                ActorId: null);
        }

        var authorization = await authorizationService
            .AuthorizeAsync(principal, resource: null, policyName)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return new CurrentUserAuthorizationResult(
            IsAuthenticated: true,
            IsAuthorized: authorization.Succeeded && !string.IsNullOrWhiteSpace(actorId),
            actorId);
    }
}
