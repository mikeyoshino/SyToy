using System.Security.Claims;

namespace ToyStore.Application.Common.Interfaces;

public interface IUserSessionValidator
{
    Task<bool> IsCurrentAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
