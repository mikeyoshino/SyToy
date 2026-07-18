using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Products.ManageProducts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ProductManagementReaderTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task PostgreSqlReaderFiltersPagesAndProjectsPrimaryMediaReferencesAndInventory()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var seeded = await SeedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ManageProductsHandler(
            scope.ServiceProvider.GetRequiredService<IProductManagementReader>());
        var cancellationToken = TestContext.Current.CancellationToken;

        var all = await handler.Handle(new ManageProductsQuery(), cancellationToken);
        var category = await handler.Handle(new ManageProductsQuery(
            ProductCategoryId: CatalogSeedIds.ArtToyCategory), cancellationToken);
        var brand = await handler.Handle(new ManageProductsQuery(BrandId: seeded.SecondBrandId), cancellationToken);
        var universe = await handler.Handle(new ManageProductsQuery(
            UniverseId: CatalogSeedIds.DcUniverse), cancellationToken);
        var published = await handler.Handle(new ManageProductsQuery(
            Status: ProductManagementStatus.Published), cancellationToken);
        var search = await handler.Handle(new ManageProductsQuery(Search: "primary-product"), cancellationToken);
        var clamped = await handler.Handle(new ManageProductsQuery(Page: 99, PageSize: 1), cancellationToken);

        Assert.True(all.IsSuccess);
        Assert.Equal(4, all.Value.TotalCount);
        var preOrder = Assert.Single(all.Value.Items, item => item.SaleType == ProductManagementSaleType.PreOrder);
        Assert.Equal(500, preOrder.DepositAmount);
        Assert.Equal(2000, preOrder.FullPrice);
        Assert.Equal(10, preOrder.TotalCapacity);
        Assert.Equal(2, preOrder.MaxPerCustomer);
        Assert.Equal(0, preOrder.OnHandQuantity);
        Assert.Contains(all.Value.Categories, option => option.Id == CatalogSeedIds.ArtToyCategory);
        Assert.Contains(all.Value.BrandFilterOptions, option => option.Id == seeded.FirstBrandId);
        Assert.Contains(all.Value.UniverseFilterOptions, option => option.Id == CatalogSeedIds.MarvelUniverse);
        Assert.Contains(all.Value.BrandFilterOptions, option => option.Id == seeded.SecondBrandId && !option.IsActive);
        Assert.DoesNotContain(all.Value.BrandEditorOptions, option => option.Id == seeded.SecondBrandId);
        Assert.Contains(all.Value.UniverseFilterOptions, option => option.Id == CatalogSeedIds.DcUniverse && !option.IsActive);
        Assert.DoesNotContain(all.Value.UniverseEditorOptions, option => option.Id == CatalogSeedIds.DcUniverse);
        Assert.Contains(category.Value.Items, item => item.Id == seeded.PrimaryProductId);
        Assert.Contains(category.Value.Items, item => item.SaleType == ProductManagementSaleType.PreOrder);
        Assert.Equal(seeded.SecondProductId, Assert.Single(brand.Value.Items).Id);
        Assert.Equal(seeded.SecondProductId, Assert.Single(universe.Value.Items).Id);
        Assert.Equal(seeded.PrimaryProductId, Assert.Single(published.Value.Items).Id);
        var projected = Assert.Single(search.Value.Items);
        Assert.Equal([seeded.FirstImageId, seeded.SecondImageId], projected.Images.Select(image => image.Id));
        Assert.True(projected.Images[0].IsPrimary);
        Assert.False(projected.Images[1].IsPrimary);
        Assert.Equal(7, projected.OnHandQuantity);
        Assert.Equal(7, projected.ReservableQuantity);
        Assert.Equal("1/12", projected.ModelScale);
        Assert.Equal(4, clamped.Value.PageNumber);
        Assert.Equal(4, clamped.Value.TotalPages);
    }

    private static async Task<Seeded> SeedAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 3, 0, 0, TimeSpan.Zero);
        var firstBrandId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var secondBrandId = Guid.Parse("91000000-0000-0000-0000-000000000002");
        db.Brands.AddRange(
            Brand.Create(firstBrandId, "แบรนด์หนึ่ง", "Brand One", CatalogSlug.Create("brand-one"), now, "test"),
            Brand.Create(secondBrandId, "แบรนด์สอง", "Brand Two", CatalogSlug.Create("brand-two"), now, "test"));

        var primaryId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var thirdId = Guid.Parse("92000000-0000-0000-0000-000000000003");
        var firstImageId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var secondImageId = Guid.Parse("93000000-0000-0000-0000-000000000002");
        var primary = Product.CreateInStock(
            primaryId, "สินค้าหลัก", "Primary Product", "รายละเอียด", "primary-product",
            CatalogSeedIds.ArtToyCategory, firstBrandId, CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1200)),
            [new ProductImageDefinition(firstImageId, "products/one.webp", "/media/products/one.webp", "ภาพหลัก"),
             new ProductImageDefinition(secondImageId, "products/two.webp", "/media/products/two.webp", "ภาพรอง")],
            [], now, "test", "1/12");
        primary.Publish(primary.Version, now.AddMinutes(1), "test");
        var second = Product.CreateInStock(
            secondId, "สินค้าสอง", "Second Product", "รายละเอียด", "second-product",
            CatalogSeedIds.GundamCategory, secondBrandId, CatalogSeedIds.DcUniverse,
            InStockOffer.Create(Money.Create(900)), [], [], now.AddMinutes(2), "test");
        var third = Product.CreateInStock(
            thirdId, "สินค้าสาม", "Third Product", "รายละเอียด", "third-product",
            CatalogSeedIds.GundamCategory, firstBrandId, CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(700)), [], [], now.AddMinutes(3), "test");
        var preOrder = Product.CreatePreOrder(
            Guid.Parse("92000000-0000-0000-0000-000000000004"), "สินค้าพรี", "Preorder Product", "รายละเอียด", "preorder-product",
            CatalogSeedIds.ArtToyCategory, firstBrandId, CatalogSeedIds.MarvelUniverse,
            PreOrderOffer.Create(Money.Create(2000), Money.Create(500), new DateOnly(2026, 12, 1),
                EstimatedArrival.Create(12, 2026), 10, 2, now), now, "test");
        db.Products.AddRange(primary, second, third, preOrder);
        var archivedBrand = await db.Brands.FindAsync([secondBrandId], TestContext.Current.CancellationToken);
        archivedBrand!.Archive(now.AddMinutes(4), "test");
        var archivedUniverse = await db.Universes.FindAsync([CatalogSeedIds.DcUniverse], TestContext.Current.CancellationToken);
        archivedUniverse!.Archive(now.AddMinutes(4), "test");
        AddInventory(db, primaryId, 7, now);
        AddInventory(db, secondId, 3, now.AddMinutes(2));
        AddInventory(db, thirdId, 0, now.AddMinutes(3));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seeded(firstBrandId, secondBrandId, primaryId, secondId, firstImageId, secondImageId);
    }

    private static void AddInventory(ApplicationDbContext db, Guid productId, int stock, DateTimeOffset now)
    {
        var creation = InventoryItem.Create(Guid.NewGuid(), productId, Guid.NewGuid(), stock,
            "สต็อกเริ่มต้น", $"product:{productId:N}", now, "test");
        db.InventoryItems.Add(creation.Item);
        db.StockMovements.Add(creation.InitialMovement);
    }

    private sealed record Seeded(Guid FirstBrandId, Guid SecondBrandId, Guid PrimaryProductId,
        Guid SecondProductId, Guid FirstImageId, Guid SecondImageId);
}
