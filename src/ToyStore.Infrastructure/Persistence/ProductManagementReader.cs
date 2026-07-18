using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Products.ManageProducts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class ProductManagementReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IProductManagementReader
{
    public async Task<ProductManagementReadPage> ReadAsync(
        ProductManagementReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Products.AsNoTracking();
        if (request.Status.HasValue)
        {
            query = query.Where(product => product.Status == request.Status.Value);
        }

        if (request.ProductCategoryId.HasValue)
        {
            query = query.Where(product => product.ProductCategoryId == request.ProductCategoryId.Value);
        }

        if (request.BrandId.HasValue)
        {
            query = query.Where(product => product.BrandId == request.BrandId.Value);
        }

        if (request.UniverseId.HasValue)
        {
            query = query.Where(product => product.UniverseId == request.UniverseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.NormalizedSearch))
        {
            var search = request.NormalizedSearch.Trim();
            query = query.Where(product =>
                product.NormalizedDisplayName.Contains(search)
                || product.NormalizedEnglishName.Contains(search)
                || EF.Functions.ILike(product.Slug, search.ToLowerInvariant()));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var effectivePage = EffectivePage(request.PageNumber, request.PageSize, totalCount);
        var products = totalCount == 0
            ? []
            : await query
                .Include(product => product.Images)
                .Include(product => product.Characters)
                .AsSplitQuery()
                .OrderByDescending(product => product.UpdatedAtUtc)
                .ThenBy(product => product.Id)
                .Skip((effectivePage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToArrayAsync(cancellationToken);

        var productIds = products.Select(product => product.Id).ToArray();
        var categoryIds = products.Select(product => product.ProductCategoryId).Distinct().ToArray();
        var brandIds = products.Select(product => product.BrandId).Distinct().ToArray();
        var universeIds = products.Select(product => product.UniverseId).Distinct().ToArray();
        var characterIds = products.SelectMany(product => product.Characters)
            .Select(link => link.CharacterId).Distinct().ToArray();

        var categoriesById = await db.ProductCategories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, cancellationToken);
        var brandsById = await db.Brands.AsNoTracking()
            .Where(brand => brandIds.Contains(brand.Id))
            .ToDictionaryAsync(brand => brand.Id, cancellationToken);
        var universesById = await db.Universes.AsNoTracking()
            .Where(universe => universeIds.Contains(universe.Id))
            .ToDictionaryAsync(universe => universe.Id, cancellationToken);
        var charactersById = await db.Characters.AsNoTracking()
            .Where(character => characterIds.Contains(character.Id))
            .ToDictionaryAsync(character => character.Id, cancellationToken);
        var inventoryByProductId = await db.InventoryItems.AsNoTracking()
            .Where(item => productIds.Contains(item.ProductId))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);

        var items = products.Select(product =>
        {
            inventoryByProductId.TryGetValue(product.Id, out var inventory);
            return new ProductManagementReadItem(
                product.Id,
                product.DisplayName,
                product.EnglishName,
                product.Description,
                product.Slug,
                product.ProductCategoryId,
                categoriesById[product.ProductCategoryId].Code,
                product.BrandId,
                brandsById[product.BrandId].DisplayName,
                product.UniverseId,
                universesById[product.UniverseId].DisplayName,
                product.InStockOffer?.Price.Amount ?? product.PreOrderOffer!.DepositAmount.Amount,
                product.Status,
                product.Version,
                inventory?.OnHandQuantity ?? 0,
                inventory?.ReservableQuantity ?? 0,
                product.Images.OrderBy(image => image.SortOrder).Select(image =>
                    new ProductManagementImage(
                        image.Id,
                        image.PublicRelativeUrl,
                        image.AltText,
                        image.SortOrder,
                        image.IsPrimary)).ToArray(),
                product.Characters.Select(link => charactersById[link.CharacterId])
                    .OrderBy(character => character.Name)
                    .Select(character => new ProductManagementCharacter(
                        character.Id,
                        character.UniverseId,
                        character.Name)).ToArray(),
                product.UpdatedAtUtc)
            {
                SaleType = product.SaleType == SaleType.InStock
                    ? ProductManagementSaleType.InStock
                    : ProductManagementSaleType.PreOrder,
                FullPrice = product.PreOrderOffer?.FullPrice.Amount,
                DepositAmount = product.PreOrderOffer?.DepositAmount.Amount,
                CloseAtUtc = product.PreOrderOffer?.CloseAtUtc,
                EstimatedArrivalMonth = product.PreOrderOffer?.EstimatedArrival.Month,
                EstimatedArrivalYear = product.PreOrderOffer?.EstimatedArrival.Year,
                TotalCapacity = product.PreOrderOffer?.TotalCapacity,
                MaxPerCustomer = product.PreOrderOffer?.MaxPerCustomer,
                BalancePaymentDays = product.PreOrderOffer?.BalancePaymentDays,
            };
        }).ToArray();

        var categories = await db.ProductCategories.AsNoTracking()
            .OrderBy(category => category.Code)
            .Select(category => new ProductManagementReferenceOption(
                category.Id,
                category.Code == "ArtToy" ? "อาร์ตทอย" : category.Code == "Gundam" ? "กันดั้ม" : category.Code,
                category.Code))
            .ToArrayAsync(cancellationToken);
        var brandFilterOptions = await db.Brands.AsNoTracking()
            .OrderBy(brand => brand.DisplayName)
            .Select(brand => new ProductManagementReferenceOption(
                brand.Id,
                brand.DisplayName,
                null,
                brand.Status == CatalogReferenceStatus.Active))
            .ToArrayAsync(cancellationToken);
        var universeFilterOptions = await db.Universes.AsNoTracking()
            .OrderBy(universe => universe.DisplayName)
            .Select(universe => new ProductManagementReferenceOption(
                universe.Id,
                universe.DisplayName,
                null,
                universe.Status == CatalogReferenceStatus.Active))
            .ToArrayAsync(cancellationToken);
        var brandEditorOptions = brandFilterOptions.Where(option => option.IsActive).ToArray();
        var universeEditorOptions = universeFilterOptions.Where(option => option.IsActive).ToArray();
        return new ProductManagementReadPage(
            items,
            categories,
            brandFilterOptions,
            universeFilterOptions,
            brandEditorOptions,
            universeEditorOptions,
            effectivePage,
            totalCount);
    }

    private static int EffectivePage(int requested, int pageSize, int totalCount) =>
        totalCount == 0 ? 1 : Math.Min(requested, (int)(((long)totalCount + pageSize - 1) / pageSize));
}
