using System.Security.Claims;
using ToyStore.Application.Common.Interfaces;

namespace ToyStore.Web.Identity;

public sealed class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
