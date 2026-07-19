using System.Data;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class StorefrontCatalogReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IStorefrontCatalogReader
{
    public async Task<StorefrontCatalogReadPage> ListAsync(
        StorefrontCatalogReadRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var brandRoute = string.IsNullOrWhiteSpace(request.BrandSlug)
            ? null
            : await db.Brands.AsNoTracking().SingleOrDefaultAsync(
                brand => brand.Slug == CatalogSlug.Create(request.BrandSlug), cancellationToken);
        var options = await ReadOptionsAsync(db, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.BrandSlug) && brandRoute is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new StorefrontCatalogReadPage(
                [], options.Categories, options.Brands, options.Characters, options.Universes,
                null, 1, 0);
        }

        IQueryable<Product> query = PriceQuery(db, request.MinimumPrice, request.MaximumPrice)
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Published);
        query = request.SaleType switch
        {
            StorefrontSaleTypeFilter.InStock => query.Where(product => product.SaleType == SaleType.InStock),
            StorefrontSaleTypeFilter.PreOrder => query.Where(product => product.SaleType == SaleType.PreOrder),
            _ => query,
        };
        if (!string.IsNullOrWhiteSpace(request.NormalizedSearch))
        {
            var search = request.NormalizedSearch.Trim();
            query = query.Where(product => product.NormalizedDisplayName.Contains(search)
                || product.NormalizedEnglishName.Contains(search)
                || EF.Functions.ILike(product.Slug, search.ToLowerInvariant()));
        }
        if (request.ProductCategoryId.HasValue)
            query = query.Where(product => product.ProductCategoryId == request.ProductCategoryId.Value);
        if (request.BrandId.HasValue)
            query = query.Where(product => product.BrandId == request.BrandId.Value);
        if (brandRoute is not null)
            query = query.Where(product => product.BrandId == brandRoute.Id);
        if (request.CharacterId.HasValue)
            query = query.Where(product => product.Characters.Any(link => link.CharacterId == request.CharacterId.Value));
        if (request.UniverseId.HasValue)
            query = query.Where(product => product.UniverseId == request.UniverseId.Value);

        var total = await query.CountAsync(cancellationToken);
        var effectivePage = EffectivePage(request.PageNumber, request.PageSize, total);
        var products = total == 0 ? [] : await query
            .Include(product => product.Images)
            .OrderByDescending(product => product.PublishedAtUtc)
            .ThenBy(product => product.Id)
            .Skip((effectivePage - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync(cancellationToken);
        var lookups = await ReadLookupsAsync(db, products, request.NowUtc, cancellationToken);
        var items = products.Select(product =>
        {
            var primary = product.Images.OrderBy(image => image.SortOrder).First();
            var availability = lookups.Availability[product.Id];
            return new StorefrontProductCard(
                product.Id,
                product.DisplayName,
                product.Slug,
                lookups.Brands[product.BrandId].DisplayName,
                CategoryLabel(lookups.Categories[product.ProductCategoryId].Code),
                ToStorefrontSaleType(product.SaleType),
                availability.State,
                FullSellingPrice(product),
                product.PreOrderOffer?.DepositAmount.Amount,
                availability.Quantity,
                primary.CardImageUrl,
                primary.AltText,
                product.ModelScale,
                product.Images.OrderBy(image => image.SortOrder).Select(image =>
                    new StorefrontProductImage(
                        image.CardImageUrl,
                        image.AltText,
                        image.SortOrder,
                        image.IsPrimary)).ToArray());
        }).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new StorefrontCatalogReadPage(
            items, options.Categories, options.Brands, options.Characters, options.Universes,
            brandRoute?.DisplayName, effectivePage, total);
    }

    public async Task<StorefrontProductDetail?> FindBySlugAsync(
        string slug,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var product = await db.Products.AsNoTracking()
            .Include(current => current.Images)
            .Include(current => current.Characters)
            .SingleOrDefaultAsync(current => current.Slug == slug
                && current.Status == ProductStatus.Published, cancellationToken);
        if (product is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var lookups = await ReadLookupsAsync(db, [product], nowUtc, cancellationToken);
        var brand = lookups.Brands[product.BrandId];
        var characterIds = product.Characters.Select(link => link.CharacterId).ToArray();
        var characters = await db.Characters.AsNoTracking()
            .Where(character => characterIds.Contains(character.Id))
            .OrderBy(character => character.Name)
            .Select(character => character.Name)
            .ToArrayAsync(cancellationToken);
        var offer = product.PreOrderOffer;
        var availability = lookups.Availability[product.Id];
        var result = new StorefrontProductDetail(
            product.Id,
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            brand.DisplayName,
            brand.Slug.Value,
            CategoryLabel(lookups.Categories[product.ProductCategoryId].Code),
            lookups.Universes[product.UniverseId].DisplayName,
            characters,
            ToStorefrontSaleType(product.SaleType),
            availability.State,
            FullSellingPrice(product),
            offer?.DepositAmount.Amount,
            offer?.BalanceAmount.Amount,
            availability.Quantity,
            offer?.CloseAtUtc,
            offer?.EstimatedArrival.Month,
            offer?.EstimatedArrival.Year,
            offer?.MaxPerCustomer,
            offer?.BalancePaymentDays,
            product.Images.OrderBy(image => image.SortOrder).Select(image =>
                new StorefrontProductImage(
                    image.PublicRelativeUrl,
                    image.AltText,
                    image.SortOrder,
                    image.IsPrimary)).ToArray(),
            product.ModelScale);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static IQueryable<Product> PriceQuery(
        ApplicationDbContext db,
        decimal? minimum,
        decimal? maximum) => (minimum, maximum) switch
        {
            ({ } min, { } max) => db.Products.FromSqlInterpolated(
                $"SELECT * FROM \"Products\" WHERE COALESCE(\"InStockPrice\", \"PreOrderFullPrice\") >= {min} AND COALESCE(\"InStockPrice\", \"PreOrderFullPrice\") <= {max}"),
            ({ } min, null) => db.Products.FromSqlInterpolated(
                $"SELECT * FROM \"Products\" WHERE COALESCE(\"InStockPrice\", \"PreOrderFullPrice\") >= {min}"),
            (null, { } max) => db.Products.FromSqlInterpolated(
                $"SELECT * FROM \"Products\" WHERE COALESCE(\"InStockPrice\", \"PreOrderFullPrice\") <= {max}"),
            _ => db.Products,
        };

    private static async Task<StorefrontOptions> ReadOptionsAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var published = db.Products.AsNoTracking().Where(product =>
            product.Status == ProductStatus.Published);
        var categories = await db.ProductCategories.AsNoTracking()
            .Where(category => published.Any(product => product.ProductCategoryId == category.Id))
            .OrderBy(category => category.Code)
            .Select(category => new StorefrontFilterOption(
                category.Id,
                category.Code == "ArtToy" ? "อาร์ตทอย" : category.Code == "Gundam" ? "กันดั้ม" : category.Code,
                null))
            .ToArrayAsync(cancellationToken);
        var brands = await db.Brands.AsNoTracking()
            .Where(brand => published.Any(product => product.BrandId == brand.Id))
            .OrderBy(brand => brand.DisplayName)
            .Select(brand => new StorefrontFilterOption(brand.Id, brand.DisplayName, brand.Slug.Value))
            .ToArrayAsync(cancellationToken);
        var universes = await db.Universes.AsNoTracking()
            .Where(universe => published.Any(product => product.UniverseId == universe.Id))
            .OrderBy(universe => universe.DisplayName)
            .Select(universe => new StorefrontFilterOption(universe.Id, universe.DisplayName, universe.Slug.Value))
            .ToArrayAsync(cancellationToken);
        var characters = await db.Characters.AsNoTracking()
            .Where(character => db.ProductCharacters.Any(link => link.CharacterId == character.Id
                && published.Any(product => product.Id == link.ProductId)))
            .OrderBy(character => character.Name)
            .Select(character => new StorefrontFilterOption(character.Id, character.Name, null))
            .ToArrayAsync(cancellationToken);
        return new StorefrontOptions(categories, brands, characters, universes);
    }

    private static async Task<StorefrontLookups> ReadLookupsAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<Product> products,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var productIds = products.Select(product => product.Id).ToArray();
        var categoryIds = products.Select(product => product.ProductCategoryId).Distinct().ToArray();
        var brandIds = products.Select(product => product.BrandId).Distinct().ToArray();
        var universeIds = products.Select(product => product.UniverseId).Distinct().ToArray();
        var categories = await db.ProductCategories.AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var brands = await db.Brands.AsNoTracking()
            .Where(x => brandIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var universes = await db.Universes.AsNoTracking()
            .Where(x => universeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var availability = productIds.ToDictionary(
            productId => productId,
            _ => new StorefrontAvailability(StorefrontOfferState.InStockOutOfStock, 0));

        var inStockIds = products.Where(x => x.SaleType == SaleType.InStock).Select(x => x.Id).ToArray();
        var inventories = await db.InventoryItems.AsNoTracking()
            .Where(x => inStockIds.Contains(x.ProductId)).ToArrayAsync(cancellationToken);
        var inventoryIds = inventories.Select(item => item.Id).ToArray();
        var stockReservations = await db.StockReservations.AsNoTracking()
            .Where(reservation => inventoryIds.Contains(reservation.InventoryItemId))
            .ToArrayAsync(cancellationToken);
        foreach (var item in inventories)
        {
            var quantity = InventoryAvailability.Calculate(
                item,
                stockReservations.Where(reservation => reservation.InventoryItemId == item.Id).ToArray(),
                nowUtc).AvailableQuantity;
            availability[item.ProductId] = new StorefrontAvailability(
                quantity > 0
                    ? StorefrontOfferState.InStockAvailable
                    : StorefrontOfferState.InStockOutOfStock,
                quantity);
        }

        var preOrderProducts = products.Where(x => x.SaleType == SaleType.PreOrder).ToArray();
        var preOrderIds = preOrderProducts.Select(x => x.Id).ToArray();
        var capacities = await db.PreOrderCapacities.AsNoTracking()
            .Where(x => preOrderIds.Contains(x.ProductId)).ToArrayAsync(cancellationToken);
        var capacityIds = capacities.Select(x => x.Id).ToArray();
        var capacityReservations = await db.PreOrderCapacityReservations.AsNoTracking()
            .Where(x => capacityIds.Contains(x.CapacityId)).ToArrayAsync(cancellationToken);
        var movements = await db.PreOrderCapacityMovements.AsNoTracking()
            .Where(x => capacityIds.Contains(x.CapacityId)).ToArrayAsync(cancellationToken);
        foreach (var product in preOrderProducts)
        {
            var owned = capacities.Where(x => x.ProductId == product.Id).ToArray();
            if (owned.Length != 1 || product.PreOrderOffer is null)
            {
                throw Incoherent("Published Pre-order Product must own exactly one capacity.");
            }

            var capacity = owned[0];
            EnsurePreOrderCoherent(
                product,
                capacity,
                capacityReservations.Where(x => x.CapacityId == capacity.Id).ToArray(),
                movements.Where(x => x.CapacityId == capacity.Id)
                    .OrderBy(x => x.ResultingCapacityVersion).ToArray());
            var state = nowUtc >= capacity.CloseAtUtc
                ? StorefrontOfferState.PreOrderClosed
                : capacity.RemainingQuantity <= 0
                    ? StorefrontOfferState.PreOrderFull
                    : StorefrontOfferState.PreOrderOpen;
            availability[product.Id] = new StorefrontAvailability(
                state,
                capacity.RemainingQuantity);
        }

        return new StorefrontLookups(categories, brands, universes, availability);
    }

    private static void EnsurePreOrderCoherent(
        Product product,
        PreOrderCapacity capacity,
        PreOrderCapacityReservation[] reservations,
        PreOrderCapacityMovement[] movements)
    {
        var offer = product.PreOrderOffer!;
        var active = reservations.Where(x => x.Status == PreOrderCapacityReservationStatus.Active)
            .Sum(x => (long)x.Quantity);
        var consumed = reservations.Where(x => x.Status == PreOrderCapacityReservationStatus.Consumed)
            .Sum(x => (long)x.Quantity);
        var retired = reservations.Where(x => x.Status == PreOrderCapacityReservationStatus.Cancelled
                && x.TransitionAtUtc >= capacity.CloseAtUtc)
            .Sum(x => (long)x.Quantity);
        var latest = movements.Length == 0 ? null : movements[^1];
        var initial = movements.Where(x => x.Type == PreOrderCapacityMovementType.InitialCapacity).ToArray();
        if (capacity.ProductId != product.Id
            || offer.CloseAtUtc.Offset != TimeSpan.Zero
            || capacity.CloseAtUtc.Offset != TimeSpan.Zero
            || offer.CloseAtUtc != capacity.CloseAtUtc
            || offer.TotalCapacity != capacity.TotalCapacity
            || reservations.Any(x => x.ProductId != product.Id)
            || movements.Any(x => x.ProductId != product.Id)
            || active != capacity.HeldQuantity
            || consumed != capacity.CommittedQuantity
            || retired != capacity.RetiredQuantity
            || (long)capacity.RemainingQuantity + capacity.HeldQuantity
                + capacity.CommittedQuantity + capacity.RetiredQuantity != capacity.TotalCapacity
            || movements.Length != capacity.Version
            || initial.Length != 1
            || initial[0].Quantity != capacity.TotalCapacity
            || latest is null
            || latest.ResultingCapacityVersion != capacity.Version
            || latest.ResultingRemainingQuantity != capacity.RemainingQuantity
            || latest.ResultingHeldQuantity != capacity.HeldQuantity
            || latest.ResultingCommittedQuantity != capacity.CommittedQuantity
            || latest.ResultingRetiredQuantity != capacity.RetiredQuantity)
        {
            throw Incoherent("Product offer, capacity, reservations and movement history do not match.");
        }
    }

    private static decimal FullSellingPrice(Product product) => product.SaleType switch
    {
        SaleType.InStock => product.InStockOffer!.Price.Amount,
        SaleType.PreOrder => product.PreOrderOffer!.FullPrice.Amount,
        _ => throw Incoherent("Product sale type is unsupported."),
    };

    private static StorefrontSaleType ToStorefrontSaleType(SaleType saleType) => saleType switch
    {
        SaleType.InStock => StorefrontSaleType.InStock,
        SaleType.PreOrder => StorefrontSaleType.PreOrder,
        _ => throw Incoherent("Product sale type is unsupported."),
    };

    private static InvalidOperationException Incoherent(string detail) => new(
        $"Persisted Storefront Pre-order data is incoherent. {detail}");

    private static int EffectivePage(int requested, int pageSize, int total) =>
        total == 0 ? 1 : Math.Min(requested, (int)(((long)total + pageSize - 1) / pageSize));

    private static string CategoryLabel(string code) => code switch
    {
        "ArtToy" => "อาร์ตทอย",
        "Gundam" => "กันดั้ม",
        _ => code,
    };

    private sealed record StorefrontAvailability(StorefrontOfferState State, int Quantity);
    private sealed record StorefrontOptions(
        IReadOnlyList<StorefrontFilterOption> Categories,
        IReadOnlyList<StorefrontFilterOption> Brands,
        IReadOnlyList<StorefrontFilterOption> Characters,
        IReadOnlyList<StorefrontFilterOption> Universes);
    private sealed record StorefrontLookups(
        IReadOnlyDictionary<Guid, ProductCategory> Categories,
        IReadOnlyDictionary<Guid, Brand> Brands,
        IReadOnlyDictionary<Guid, Universe> Universes,
        IReadOnlyDictionary<Guid, StorefrontAvailability> Availability);
}
