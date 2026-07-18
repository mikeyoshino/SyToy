namespace ToyStore.Infrastructure.Identity;

public interface IIdentityInitializer
{
    Task SeedRolesAsync(CancellationToken cancellationToken);
}
