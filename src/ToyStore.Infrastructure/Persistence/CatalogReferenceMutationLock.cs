using Microsoft.EntityFrameworkCore;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CatalogReferenceMutationLock(ApplicationDbContext dbContext)
{
    private const long BrandScope = 0x5453594252414E44;
    private const long ProductScope = 0x54535950524F4455;
    private const long UniverseScope = 0x545359554E495652;

    public Task<int> AcquireBrandAsync(CancellationToken cancellationToken) =>
        AcquireAsync(BrandScope, cancellationToken);

    public Task<int> AcquireUniverseAsync(CancellationToken cancellationToken) =>
        AcquireAsync(UniverseScope, cancellationToken);

    public Task<int> AcquireProductAsync(CancellationToken cancellationToken) =>
        AcquireAsync(ProductScope, cancellationToken);

    private Task<int> AcquireAsync(long scope, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A catalog-reference mutation lock requires an active database transaction.");
        }

        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({scope})",
            cancellationToken);
    }
}
