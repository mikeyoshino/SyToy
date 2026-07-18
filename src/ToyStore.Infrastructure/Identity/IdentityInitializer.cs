using Microsoft.AspNetCore.Identity;
using ToyStore.Application.Common.Authorization;

namespace ToyStore.Infrastructure.Identity;

public sealed class IdentityInitializer(RoleManager<IdentityRole> roleManager)
    : IIdentityInitializer
{
    public async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleNames.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not initialize required Identity role '{roleName}'.");
            }
        }
    }
}
