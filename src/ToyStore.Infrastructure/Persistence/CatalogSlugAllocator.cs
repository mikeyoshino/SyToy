using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Catalog.Slugs;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

public sealed class CatalogSlugAllocator(ApplicationDbContext dbContext)
    : ICatalogSlugAllocator
{
    private const long ProductScopeLock = 0x54535950524F4455;
    private const long BrandScopeLock = 0x5453594252414E44;
    private const long UniverseScopeLock = 0x545359554E495652;

    public Task<CatalogSlug> AllocateProductAsync(
        string englishName,
        CancellationToken cancellationToken) =>
        AllocateAsync(
            englishName,
            ProductScopeLock,
            baseValue => dbContext.Products
                .Where(product => product.Slug == baseValue || product.Slug.StartsWith(baseValue + "-"))
                .Select(product => product.Slug)
                .ToListAsync(cancellationToken),
            cancellationToken);

    internal Task<CatalogSlug> AllocateProductForLockedMutationAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        AllocateWithoutLockAsync(
            englishName,
            baseValue => dbContext.Products
                .Where(product => (!excludedId.HasValue || product.Id != excludedId.Value)
                    && (product.Slug == baseValue || product.Slug.StartsWith(baseValue + "-")))
                .Select(product => product.Slug)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public Task<CatalogSlug> AllocateBrandAsync(
        string englishName,
        CancellationToken cancellationToken) =>
        AllocateBrandAsync(englishName, null, cancellationToken);

    internal Task<CatalogSlug> AllocateBrandAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        AllocateAsync(
            englishName,
            BrandScopeLock,
            async baseValue => (await dbContext.Brands
                    .Where(brand => !excludedId.HasValue || brand.Id != excludedId.Value)
                    .Select(brand => brand.Slug)
                    .ToListAsync(cancellationToken))
                .Select(slug => slug.Value)
                .Where(value => value == baseValue || value.StartsWith(baseValue + "-", StringComparison.Ordinal))
                .ToList(),
            cancellationToken);

    internal Task<CatalogSlug> AllocateBrandForLockedMutationAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        AllocateWithoutLockAsync(
            englishName,
            async baseValue => (await dbContext.Brands
                    .Where(brand => !excludedId.HasValue || brand.Id != excludedId.Value)
                    .Select(brand => brand.Slug)
                    .ToListAsync(cancellationToken))
                .Select(slug => slug.Value)
                .Where(value => value == baseValue || value.StartsWith(baseValue + "-", StringComparison.Ordinal))
                .ToList(),
            cancellationToken);

    public Task<CatalogSlug> AllocateUniverseAsync(
        string englishName,
        CancellationToken cancellationToken) =>
        AllocateUniverseAsync(englishName, null, cancellationToken);

    internal Task<CatalogSlug> AllocateUniverseAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        AllocateAsync(
            englishName,
            UniverseScopeLock,
            async baseValue => (await dbContext.Universes
                    .Where(universe => !excludedId.HasValue || universe.Id != excludedId.Value)
                    .Select(universe => universe.Slug)
                    .ToListAsync(cancellationToken))
                .Select(slug => slug.Value)
                .Where(value => value == baseValue || value.StartsWith(baseValue + "-", StringComparison.Ordinal))
                .ToList(),
            cancellationToken);

    internal Task<CatalogSlug> AllocateUniverseForLockedMutationAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        AllocateWithoutLockAsync(
            englishName,
            async baseValue => (await dbContext.Universes
                    .Where(universe => !excludedId.HasValue || universe.Id != excludedId.Value)
                    .Select(universe => universe.Slug)
                    .ToListAsync(cancellationToken))
                .Select(slug => slug.Value)
                .Where(value => value == baseValue || value.StartsWith(baseValue + "-", StringComparison.Ordinal))
                .ToList(),
            cancellationToken);

    private async Task<CatalogSlug> AllocateAsync(
        string englishName,
        long scopeLock,
        Func<string, Task<List<string>>> loadCandidates,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Catalog slug allocation requires an active database transaction.");
        }

        var baseSlug = CatalogSlugGenerator.GenerateBase(englishName).Value;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({scopeLock})",
            cancellationToken);

        return await AllocateFromCandidatesAsync(
            baseSlug,
            loadCandidates,
            cancellationToken);
    }

    private async Task<CatalogSlug> AllocateWithoutLockAsync(
        string englishName,
        Func<string, Task<List<string>>> loadCandidates,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Catalog slug allocation requires an active database transaction.");
        }

        var baseSlug = CatalogSlugGenerator.GenerateBase(englishName).Value;
        return await AllocateFromCandidatesAsync(
            baseSlug,
            loadCandidates,
            cancellationToken);
    }

    private static async Task<CatalogSlug> AllocateFromCandidatesAsync(
        string baseSlug,
        Func<string, Task<List<string>>> loadCandidates,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await loadCandidates(baseSlug);
        if (!existing.Contains(baseSlug, StringComparer.Ordinal))
        {
            return CatalogSlug.Create(baseSlug);
        }

        var usedSuffixes = new HashSet<int>();
        var prefix = baseSlug + "-";
        foreach (var value in existing)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(
                    value.AsSpan(prefix.Length),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var suffix)
                && suffix >= 2)
            {
                usedSuffixes.Add(suffix);
            }
        }

        var nextSuffix = 2;
        while (usedSuffixes.Contains(nextSuffix))
        {
            nextSuffix++;
        }

        return CatalogSlug.Create(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{baseSlug}-{nextSuffix}"));
    }
}
