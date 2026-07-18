using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.ListBrands;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Brands;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ListBrandsQueryTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task QuerySupportsStatusNormalizedSearchStablePagingCountsAndCanonicalClamp()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var seeded = await SeedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ListBrandsHandler(
            scope.ServiceProvider.GetRequiredService<IBrandListReader>());

        var defaultActive = await handler.Handle(
            new ListBrandsQuery(),
            TestContext.Current.CancellationToken);
        var archived = await handler.Handle(
            new ListBrandsQuery(Status: CatalogReferenceListStatus.Archived),
            TestContext.Current.CancellationToken);
        var all = await handler.Handle(
            new ListBrandsQuery(Status: CatalogReferenceListStatus.All),
            TestContext.Current.CancellationToken);
        var displaySearch = await handler.Handle(
            new ListBrandsQuery("  บันได  "),
            TestContext.Current.CancellationToken);
        var englishSearch = await handler.Handle(
            new ListBrandsQuery("  bandai  "),
            TestContext.Current.CancellationToken);
        var slugSearch = await handler.Handle(
            new ListBrandsQuery("bandai-spirits"),
            TestContext.Current.CancellationToken);
        var clamped = await handler.Handle(
            new ListBrandsQuery(
                "bandai",
                CatalogReferenceListStatus.Active,
                Page: 99,
                PageSize: 1),
            TestContext.Current.CancellationToken);
        var zero = await handler.Handle(
            new ListBrandsQuery("ไม่พบแน่นอน"),
            TestContext.Current.CancellationToken);

        Assert.True(defaultActive.IsSuccess);
        Assert.Equal(2, defaultActive.Value.TotalCount);
        Assert.Equal([seeded.FirstActiveId, seeded.SecondActiveId],
            defaultActive.Value.Items.Select(item => item.Id));
        Assert.Equal(2, defaultActive.Value.Items[0].ProductReferenceCount);
        Assert.Equal(1, defaultActive.Value.Items[1].ProductReferenceCount);
        Assert.True(defaultActive.Value.Items[0].CanBeUsedByPublishedProduct);
        Assert.False(defaultActive.Value.Items[1].CanBeUsedByPublishedProduct);

        var archivedItem = Assert.Single(archived.Value.Items);
        Assert.Equal(seeded.ArchivedId, archivedItem.Id);
        Assert.Equal(CatalogReferenceStatus.Archived, archivedItem.Status);
        Assert.Equal([seeded.ArchivedId, seeded.FirstActiveId, seeded.SecondActiveId],
            all.Value.Items.Select(item => item.Id));

        Assert.Equal(2, displaySearch.Value.TotalCount);
        Assert.Equal(2, englishSearch.Value.TotalCount);
        Assert.Equal(seeded.SecondActiveId, Assert.Single(slugSearch.Value.Items).Id);
        Assert.Equal(2, clamped.Value.TotalCount);
        Assert.Equal(2, clamped.Value.PageNumber);
        Assert.Equal(2, clamped.Value.TotalPages);
        Assert.Equal(seeded.SecondActiveId, Assert.Single(clamped.Value.Items).Id);

        Assert.Equal(0, zero.Value.TotalCount);
        Assert.Equal(0, zero.Value.TotalPages);
        Assert.Equal(1, zero.Value.PageNumber);
        Assert.Empty(zero.Value.Items);
    }

    private static async Task<SeededBrands> SeedAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);
        var firstId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("81000000-0000-0000-0000-000000000002");
        var archivedId = Guid.Parse("81000000-0000-0000-0000-000000000003");
        var first = Brand.CreateWithImage(
            firstId,
            "บันได โมเดล",
            "Bandai Models",
            CatalogSlug.Create("bandai-models"),
            CatalogMediaReference.Create(
                "brands/bandai-models.webp",
                "/media/brands/bandai-models.webp",
                "โลโก้แบรนด์ บันได โมเดล"),
            now,
            "test");
        var second = Brand.Create(
            secondId,
            "บันได สปิริตส์",
            "Bandai Spirits",
            CatalogSlug.Create("bandai-spirits"),
            now,
            "test");
        var archived = Brand.CreateWithImage(
            archivedId,
            "แบรนด์เก็บถาวร",
            "Archived House",
            CatalogSlug.Create("archived-house"),
            CatalogMediaReference.Create(
                "brands/archived-house.webp",
                "/media/brands/archived-house.webp",
                "โลโก้แบรนด์ แบรนด์เก็บถาวร"),
            now,
            "test");
        archived.Archive(now.AddMinutes(1), "test");
        dbContext.Brands.AddRange(first, second, archived);
        dbContext.Products.AddRange(
            Product(firstId, 1, firstId, now),
            Product(firstId, 2, firstId, now),
            Product(secondId, 3, secondId, now));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededBrands(firstId, secondId, archivedId);
    }

    private static Product Product(
        Guid seed,
        int sequence,
        Guid brandId,
        DateTimeOffset createdAtUtc) =>
        ToyStore.Domain.Products.Product.CreateInStock(
            new Guid(seed.ToByteArray().Select((value, index) =>
                index == 15 ? (byte)sequence : value).ToArray()),
            $"สินค้า {sequence}",
            $"Product {sequence}",
            "รายละเอียด",
            $"product-{sequence}",
            CatalogSeedIds.GundamCategory,
            brandId,
            CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)),
            createdAtUtc,
            "test");

    private sealed record SeededBrands(
        Guid FirstActiveId,
        Guid SecondActiveId,
        Guid ArchivedId);
}
