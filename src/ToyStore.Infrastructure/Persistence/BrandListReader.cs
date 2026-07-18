using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Brands;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class BrandListReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IBrandListReader
{
    public async Task<BrandListReadPage> ReadAsync(
        BrandListReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.PageNumber, request.PageSize);

        await using var dbContext = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = dbContext.Brands.AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(brand => brand.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedSearch))
        {
            var normalizedSearch = request.NormalizedSearch.Trim();
            var possibleSlug = normalizedSearch.ToLowerInvariant();
            if (CatalogSlug.IsValid(possibleSlug))
            {
                var slug = CatalogSlug.Create(possibleSlug);
                query = query.Where(brand =>
                    brand.NormalizedDisplayName.Contains(normalizedSearch)
                    || brand.NormalizedEnglishName.Contains(normalizedSearch)
                    || brand.Slug == slug);
            }
            else
            {
                query = query.Where(brand =>
                    brand.NormalizedDisplayName.Contains(normalizedSearch)
                    || brand.NormalizedEnglishName.Contains(normalizedSearch));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePageNumber = EffectivePageNumber(
            request.PageNumber,
            request.PageSize,
            totalCount);

        if (totalCount == 0)
        {
            return new BrandListReadPage([], effectivePageNumber, totalCount);
        }

        var rows = await query
            .OrderByDescending(brand => brand.UpdatedAtUtc)
            .ThenBy(brand => brand.Id)
            .Skip((effectivePageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(brand => new BrandReadRow(
                brand,
                dbContext.Products.Count(product => product.BrandId == brand.Id)))
            .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(row => new BrandListReadItem(
                row.Brand.Id,
                row.Brand.DisplayName,
                row.Brand.EnglishName,
                row.Brand.Slug.Value,
                row.Brand.Image?.PublicRelativeUrl,
                row.Brand.Image?.AltText,
                row.Brand.Status,
                row.Brand.CanBeUsedByPublishedProduct,
                row.Brand.Version,
                row.ProductReferenceCount,
                row.Brand.UpdatedAtUtc))
            .ToArray();
        return new BrandListReadPage(items, effectivePageNumber, totalCount);
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

    private sealed record BrandReadRow(Brand Brand, int ProductReferenceCount);
}
