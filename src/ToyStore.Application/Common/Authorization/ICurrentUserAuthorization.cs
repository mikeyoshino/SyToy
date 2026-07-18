namespace ToyStore.Application.Common.Authorization;

public interface ICurrentUserAuthorization
{
    Task<CurrentUserAuthorizationResult> AuthorizeAsync(
        string policyName,
        CancellationToken cancellationToken);
}
