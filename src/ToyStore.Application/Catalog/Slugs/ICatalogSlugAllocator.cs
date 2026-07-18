using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Catalog.Slugs;

public interface ICatalogSlugAllocator
{
    Task<CatalogSlug> AllocateProductAsync(
        string englishName,
        CancellationToken cancellationToken);

    Task<CatalogSlug> AllocateBrandAsync(
        string englishName,
        CancellationToken cancellationToken);

    Task<CatalogSlug> AllocateUniverseAsync(
        string englishName,
        CancellationToken cancellationToken);
}
