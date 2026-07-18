using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class StorefrontCatalogReaderTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublicListHardGatesLifecycleAndSupportsEveryUrlFilterPricePagingAndAvailability()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var seeded = await SeedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ListStorefrontProductsHandler(
            scope.ServiceProvider.GetRequiredService<IStorefrontCatalogReader>(), new FixedTimeProvider(Now));
        var token = TestContext.Current.CancellationToken;

        var all = await handler.Handle(new ListStorefrontProductsQuery(), token);
        var combined = await handler.Handle(new ListStorefrontProductsQuery(
            SaleType: StorefrontSaleTypeFilter.InStock,
            ProductCategoryId: CatalogSeedIds.ArtToyCategory,
            BrandId: seeded.BrandId,
            CharacterId: seeded.CharacterId,
            UniverseId: CatalogSeedIds.MarvelUniverse,
            MinimumPrice: 900,
            MaximumPrice: 1100), token);
        var brandRoute = await handler.Handle(new ListStorefrontProductsQuery(BrandSlug: "public-brand"), token);
        var preOrder = await handler.Handle(new ListStorefrontProductsQuery(SaleType: StorefrontSaleTypeFilter.PreOrder), token);
        var clamped = await handler.Handle(new ListStorefrontProductsQuery(Page: 99, PageSize: 1), token);

        Assert.True(all.IsSuccess);
        Assert.Equal(2, all.Value.TotalCount);
        Assert.DoesNotContain(all.Value.Items, item => item.Id == seeded.DraftId || item.Id == seeded.ArchivedId);
        Assert.Contains(all.Value.Items, item => item.Slug == "second-published" && !item.IsAvailable);
        var card = Assert.Single(combined.Value.Items);
        Assert.Equal(seeded.PublishedId, card.Id);
        Assert.True(card.IsAvailable);
        Assert.Equal(seeded.ThumbnailImageUrl, card.PrimaryImageUrl);
        Assert.Equal("ภาพหลักสำหรับหน้าร้าน", card.PrimaryImageAltText);
        Assert.Equal("แบรนด์หน้าร้าน", brandRoute.Value.BrandDisplayName);
        Assert.Equal(2, brandRoute.Value.TotalCount);
        Assert.Empty(preOrder.Value.Items);
        Assert.Equal(2, clamped.Value.PageNumber);
        Assert.Equal(2, clamped.Value.TotalPages);
        Assert.Contains(all.Value.Characters, option => option.Id == seeded.CharacterId);
    }

    [Fact]
    public async Task DetailReturnsPublishedInStockWithOrderedGalleryAndEffectiveAvailabilityOnly()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var seeded = await SeedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new GetStorefrontProductHandler(
            scope.ServiceProvider.GetRequiredService<IStorefrontCatalogReader>(), new FixedTimeProvider(Now));

        var detail = await handler.Handle(
            new GetStorefrontProductQuery("published-product"), TestContext.Current.CancellationToken);
        var draft = await handler.Handle(
            new GetStorefrontProductQuery("draft-product"), TestContext.Current.CancellationToken);
        var archived = await handler.Handle(
            new GetStorefrontProductQuery("archived-product"), TestContext.Current.CancellationToken);

        Assert.True(detail.IsSuccess);
        Assert.Equal(4, detail.Value.AvailableQuantity); // expired hold is excluded; live hold remains.
        Assert.Equal([seeded.PrimaryImageUrl, seeded.SecondaryImageUrl], detail.Value.Images.Select(image => image.Url));
        Assert.True(detail.Value.Images[0].IsPrimary);
        Assert.Equal(StorefrontCatalogErrors.ProductNotFound, draft.Error);
        Assert.Equal(StorefrontCatalogErrors.ProductNotFound, archived.Error);

        using var publishedResponse = await client.GetAsync(
            "/products/published-product", TestContext.Current.CancellationToken);
        using var draftResponse = await client.GetAsync(
            "/products/draft-product", TestContext.Current.CancellationToken);
        using var archivedResponse = await client.GetAsync(
            "/products/archived-product", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, publishedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archivedResponse.StatusCode);
    }

    [Fact]
    public async Task PublishedPreOrderUsesFullPriceForFiltersAndProjectsOpenFullClosedAndRequiredDetailFields()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var open = await SeedPreOrderAsync(factory, "open-preorder", 2500, 500,
            new DateOnly(2026, 8, 1), 4, consumeAll: false);
        var full = await SeedPreOrderAsync(factory, "full-preorder", 3000, 600,
            new DateOnly(2026, 8, 1), 1, consumeAll: true);
        var closed = await SeedPreOrderAsync(factory, "closed-preorder", 3500, 700,
            new DateOnly(2026, 7, 16), 2, consumeAll: false);
        var before = await PreOrderCountsAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStorefrontCatalogReader>();

        var filtered = await reader.ListAsync(new StorefrontCatalogReadRequest(
            null, StorefrontSaleTypeFilter.PreOrder, null, null, null, null, null,
            2400, 2600, 1, 12, Now), TestContext.Current.CancellationToken);
        var all = await reader.ListAsync(new StorefrontCatalogReadRequest(
            null, StorefrontSaleTypeFilter.PreOrder, null, null, null, null, null,
            null, null, 1, 12, Now), TestContext.Current.CancellationToken);
        var detail = await reader.FindBySlugAsync(open.Slug, Now, TestContext.Current.CancellationToken);
        var after = await PreOrderCountsAsync(factory);

        var filteredCard = Assert.Single(filtered.Items);
        Assert.Equal(open.ProductId, filteredCard.Id);
        Assert.Equal(StorefrontSaleType.PreOrder, filteredCard.SaleType);
        Assert.Equal(2500, filteredCard.Price);
        Assert.Equal(500, filteredCard.DepositAmount);
        Assert.Equal(StorefrontOfferState.PreOrderOpen, filteredCard.OfferState);
        Assert.Contains(all.Items, card => card.Id == full.ProductId &&
            card.OfferState == StorefrontOfferState.PreOrderFull && card.AvailableQuantity == 0);
        Assert.Contains(all.Items, card => card.Id == closed.ProductId &&
            card.OfferState == StorefrontOfferState.PreOrderClosed);
        Assert.NotNull(detail);
        Assert.Equal(StorefrontSaleType.PreOrder, detail.SaleType);
        Assert.Equal(StorefrontOfferState.PreOrderOpen, detail.OfferState);
        Assert.Equal(2500, detail.Price);
        Assert.Equal(500, detail.DepositAmount);
        Assert.Equal(2000, detail.BalanceAmount);
        Assert.Equal(4, detail.AvailableQuantity);
        Assert.Equal(open.CloseAtUtc, detail.PreOrderCloseAtUtc);
        Assert.Equal(12, detail.EstimatedArrivalMonth);
        Assert.Equal(2026, detail.EstimatedArrivalYear);
        Assert.Equal(3, detail.MaxPerCustomer);
        Assert.Equal(7, detail.BalancePaymentDays);
        Assert.Equal(before, after);

        using var response = await client.GetAsync($"/products/{open.Slug}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("พรีออเดอร์", html, StringComparison.Ordinal);
        Assert.Contains("มัดจำ", html, StringComparison.Ordinal);
        Assert.Contains("ยอดคงเหลือ", html, StringComparison.Ordinal);
        Assert.Contains("ปิดรับพรีออเดอร์", html, StringComparison.Ordinal);
        Assert.Contains("สินค้าคาดว่าจะมา", html, StringComparison.Ordinal);
        Assert.DoesNotContain("สูงสุดต่อคน", html, StringComparison.Ordinal);
        Assert.Contains("มัดจำไม่คืน", html, StringComparison.Ordinal);
        Assert.DoesNotContain("เพิ่มลงตะกร้า", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishedPreOrderWithoutCoherentCapacityFailsClosed()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var missing = await SeedPreOrderAsync(factory, "missing-capacity", 1000, 200,
            new DateOnly(2026, 8, 1), 2, consumeAll: false, includeCapacity: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStorefrontCatalogReader>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.FindBySlugAsync(
            missing.Slug, Now, TestContext.Current.CancellationToken));
    }

    private static async Task<Seeded> SeedAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brandId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var characterId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
        db.Brands.Add(Brand.Create(brandId, "แบรนด์หน้าร้าน", "Public Brand", CatalogSlug.Create("public-brand"), Now.AddDays(-2), "test"));
        db.Characters.Add(Character.Create(characterId, CatalogSeedIds.MarvelUniverse, "ฮีโร่ทดสอบ"));
        var publishedId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
        var draftId = Guid.Parse("a3000000-0000-0000-0000-000000000002");
        var archivedId = Guid.Parse("a3000000-0000-0000-0000-000000000003");
        var secondPublishedId = Guid.Parse("a3000000-0000-0000-0000-000000000004");
        const string primaryUrl = "/media/public/primary.webp";
        const string secondaryUrl = "/media/public/secondary.webp";
        const string thumbnailUrl = "/media/public/thumbnail.webp";
        Product Create(Guid id, string name, string slug, decimal price, Guid category, Guid universe, DateTimeOffset created) =>
            Product.CreateInStock(id, name, slug.Replace('-', ' '), "รายละเอียดสินค้าสำหรับหน้าร้าน", slug,
                category, brandId, universe, InStockOffer.Create(Money.Create(price)),
                [new ProductImageDefinition(Guid.NewGuid(), $"{slug}/primary.webp", primaryUrl, "ภาพหลักสำหรับหน้าร้าน",
                    $"{slug}/thumbnail.webp", thumbnailUrl),
                 new ProductImageDefinition(Guid.NewGuid(), $"{slug}/secondary.webp", secondaryUrl, "ภาพด้านข้างสินค้า")],
                slug == "published-product" ? [characterId] : [], created, "test");
        var published = Create(publishedId, "สินค้าเผยแพร่", "published-product", 1000,
            CatalogSeedIds.ArtToyCategory, CatalogSeedIds.MarvelUniverse, Now.AddDays(-1));
        published.Publish(published.Version, Now.AddHours(-4), "test");
        var draft = Create(draftId, "สินค้าร่าง", "draft-product", 1000,
            CatalogSeedIds.ArtToyCategory, CatalogSeedIds.MarvelUniverse, Now.AddDays(-1));
        var archived = Create(archivedId, "สินค้าเก็บ", "archived-product", 1000,
            CatalogSeedIds.ArtToyCategory, CatalogSeedIds.MarvelUniverse, Now.AddDays(-1));
        archived.Publish(archived.Version, Now.AddHours(-3), "test");
        archived.Archive(archived.Version, Now.AddHours(-2), "test");
        var secondPublished = Create(secondPublishedId, "สินค้าสอง", "second-published", 2000,
            CatalogSeedIds.GundamCategory, CatalogSeedIds.DcUniverse, Now.AddDays(-1));
        secondPublished.Publish(secondPublished.Version, Now.AddHours(-1), "test");
        db.Products.AddRange(published, draft, archived, secondPublished);
        var publishedInventory = AddInventory(db, publishedId, 6);
        AddInventory(db, draftId, 2);
        AddInventory(db, archivedId, 2);
        var expired = publishedInventory.Reserve(Guid.NewGuid(), Guid.NewGuid(), 1,
            Now.AddHours(-2), Now.AddMinutes(-1), "checkout", "expired", publishedInventory.Version, "test");
        var live = publishedInventory.Reserve(Guid.NewGuid(), Guid.NewGuid(), 2,
            Now.AddHours(-1), Now.AddHours(1), "checkout", "live", publishedInventory.Version, "test");
        db.StockReservations.AddRange(expired, live);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Seeded(brandId, characterId, publishedId, draftId, archivedId,
            primaryUrl, secondaryUrl, thumbnailUrl);
    }

    private static InventoryItem AddInventory(ApplicationDbContext db, Guid productId, int stock)
    {
        var creation = InventoryItem.Create(Guid.NewGuid(), productId, Guid.NewGuid(), stock,
            "สต็อกเริ่มต้น", $"product:{productId:N}", Now.AddDays(-1), "test");
        db.InventoryItems.Add(creation.Item);
        db.StockMovements.Add(creation.InitialMovement);
        return creation.Item;
    }

    private static async Task<SeededPreOrder> SeedPreOrderAsync(
        ToyStoreWebApplicationFactory factory,
        string slug,
        decimal fullPrice,
        decimal deposit,
        DateOnly closeDate,
        int capacity,
        bool consumeAll,
        bool includeCapacity = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(Guid.NewGuid(), $"แบรนด์ {slug}", $"Brand {slug}",
            CatalogSlug.Create($"brand-{slug}"), Now.AddMonths(-1), "test");
        var offer = PreOrderOffer.Create(Money.Create(fullPrice), Money.Create(deposit), closeDate,
            EstimatedArrival.Create(12, 2026), capacity, Math.Min(3, capacity), Now.AddMonths(-1), 7);
        var product = Product.CreatePreOrder(Guid.NewGuid(), $"สินค้า {slug}", $"Product {slug}",
            "รายละเอียดพรีออเดอร์", slug, CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, offer,
            [new ProductImageDefinition(Guid.NewGuid(), $"{slug}/primary.webp",
                $"/media/{slug}/primary.webp", "ภาพพรีออเดอร์")], [], Now.AddMonths(-1), "test");
        product.Publish(product.Version, Now.AddDays(-20), "test");
        db.AddRange(brand, product);
        if (includeCapacity)
        {
            var creation = PreOrderCapacity.Create(Guid.NewGuid(), product.Id, Guid.NewGuid(), offer,
                "เปิดรอบ", $"product:{product.Id:N}", Now.AddDays(-20), "test");
            db.PreOrderCapacities.Add(creation.Capacity);
            db.PreOrderCapacityMovements.Add(creation.Movement);
            if (consumeAll)
            {
                var reserved = creation.Capacity.Reserve(Guid.NewGuid(), Guid.NewGuid(), "customer-1",
                    capacity, Now.AddDays(-10), Now.AddDays(-10).AddMinutes(30), Guid.NewGuid(),
                    "จอง", $"product:{product.Id:N}", creation.Capacity.Version, "customer-1");
                var consumed = creation.Capacity.ConsumeReservation(reserved.Reservation, Guid.NewGuid(),
                    "รับมัดจำ", $"payment:{product.Id:N}", creation.Capacity.Version,
                    Now.AddDays(-10).AddMinutes(5), "payment-system");
                db.PreOrderCapacityReservations.Add(reserved.Reservation);
                db.PreOrderCapacityMovements.AddRange(reserved.Movement, consumed.Movement!);
            }
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededPreOrder(product.Id, slug, offer.CloseAtUtc);
    }

    private static async Task<PreOrderCounts> PreOrderCountsAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new PreOrderCounts(
            await db.PreOrderCapacities.CountAsync(TestContext.Current.CancellationToken),
            await db.PreOrderCapacityReservations.CountAsync(TestContext.Current.CancellationToken),
            await db.PreOrderCapacityMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    private sealed record Seeded(Guid BrandId, Guid CharacterId, Guid PublishedId, Guid DraftId,
        Guid ArchivedId, string PrimaryImageUrl, string SecondaryImageUrl, string ThumbnailImageUrl);
    private sealed record SeededPreOrder(Guid ProductId, string Slug, DateTimeOffset CloseAtUtc);
    private sealed record PreOrderCounts(int Capacities, int Reservations, int Movements);
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
