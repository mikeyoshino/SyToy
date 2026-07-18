using Microsoft.AspNetCore.Identity;

namespace ToyStore.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public bool MustChangePassword { get; set; }
}
