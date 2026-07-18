using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Universes;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class UniverseListReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IUniverseListReader
{
    public async Task<UniverseListReadPage> ReadAsync(
        UniverseListReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.PageNumber, request.PageSize);

        await using var dbContext = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = dbContext.Universes.AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(universe => universe.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedSearch))
        {
            var normalizedSearch = request.NormalizedSearch.Trim();
            var possibleSlug = normalizedSearch.ToLowerInvariant();
            if (CatalogSlug.IsValid(possibleSlug))
            {
                var slug = CatalogSlug.Create(possibleSlug);
                query = query.Where(universe =>
                    universe.NormalizedDisplayName.Contains(normalizedSearch)
                    || universe.NormalizedEnglishName.Contains(normalizedSearch)
                    || universe.Slug == slug);
            }
            else
            {
                query = query.Where(universe =>
                    universe.NormalizedDisplayName.Contains(normalizedSearch)
                    || universe.NormalizedEnglishName.Contains(normalizedSearch));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePageNumber = EffectivePageNumber(
            request.PageNumber,
            request.PageSize,
            totalCount);

        if (totalCount == 0)
        {
            return new UniverseListReadPage([], effectivePageNumber, totalCount);
        }

        var rows = await query
            .OrderByDescending(universe => universe.UpdatedAtUtc)
            .ThenBy(universe => universe.Id)
            .Skip((effectivePageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(universe => new UniverseReadRow(
                universe,
                dbContext.Products.Count(product => product.UniverseId == universe.Id),
                dbContext.Characters.Count(character => character.UniverseId == universe.Id)))
            .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(row => new UniverseListReadItem(
                row.Universe.Id,
                row.Universe.DisplayName,
                row.Universe.EnglishName,
                row.Universe.Slug.Value,
                row.Universe.Logo?.PublicRelativeUrl,
                row.Universe.Logo?.AltText,
                row.Universe.Status,
                row.Universe.CanBeUsedByPublishedProduct,
                row.Universe.Version,
                row.ProductReferenceCount,
                row.CharacterReferenceCount,
                row.Universe.UpdatedAtUtc))
            .ToArray();
        return new UniverseListReadPage(items, effectivePageNumber, totalCount);
    }

    private static int EffectivePageNumber(int requestedPage, int pageSize, int totalCount)
    {
        if (totalCount == 0)
        {
            return 1;
        }

        var lastPage = (int)(((long)totalCount + pageSize - 1) / pageSize);
        return Math.Min(requestedPage, lastPage);
    }

    private static void ValidatePage(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    }

    private sealed record UniverseReadRow(
        Universe Universe,
        int ProductReferenceCount,
        int CharacterReferenceCount);
}
