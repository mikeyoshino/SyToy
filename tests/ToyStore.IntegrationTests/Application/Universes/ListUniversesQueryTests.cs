using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ListUniverses;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Universes;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ListUniversesQueryTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task QueryPreservesFixedSeedsAndSupportsCountsSearchOrderingZeroAndClamp()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var archivedId = await SeedReferencesAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ListUniversesHandler(
            scope.ServiceProvider.GetRequiredService<IUniverseListReader>());

        var defaultActive = await handler.Handle(
            new ListUniversesQuery(),
            TestContext.Current.CancellationToken);
        var archived = await handler.Handle(
            new ListUniversesQuery(Status: CatalogReferenceListStatus.Archived),
            TestContext.Current.CancellationToken);
        var all = await handler.Handle(
            new ListUniversesQuery(Status: CatalogReferenceListStatus.All),
            TestContext.Current.CancellationToken);
        var displaySearch = await handler.Handle(
            new ListUniversesQuery(
                "  จักรวาลเก็บถาวร  ",
                CatalogReferenceListStatus.All),
            TestContext.Current.CancellationToken);
        var englishSearch = await handler.Handle(
            new ListUniversesQuery(
                "  Ａｒｃｈｉｖｅｄ  ",
                CatalogReferenceListStatus.All),
            TestContext.Current.CancellationToken);
        var slugSearch = await handler.Handle(
            new ListUniversesQuery("dc"),
            TestContext.Current.CancellationToken);
        var clamped = await handler.Handle(
            new ListUniversesQuery(
                Status: CatalogReferenceListStatus.Active,
                Page: 99,
                PageSize: 2),
            TestContext.Current.CancellationToken);
        var zero = await handler.Handle(
            new ListUniversesQuery("ไม่พบจักรวาล"),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, defaultActive.Value.TotalCount);
        Assert.Equal(
            [CatalogSeedIds.MarvelUniverse, CatalogSeedIds.DcUniverse, CatalogSeedIds.UnknownUniverse],
            defaultActive.Value.Items.Select(item => item.Id));
        Assert.All(defaultActive.Value.Items, item =>
        {
            Assert.Equal(CatalogReferenceStatus.Active, item.Status);
            Assert.Null(item.LogoPublicRelativeUrl);
            Assert.Null(item.LogoAltText);
            Assert.False(item.CanBeUsedByPublishedProduct);
            Assert.Equal(1, item.Version);
        });
        var marvel = defaultActive.Value.Items[0];
        Assert.Equal(2, marvel.ProductReferenceCount);
        Assert.Equal(2, marvel.CharacterReferenceCount);
        Assert.Equal(0, defaultActive.Value.Items[1].ProductReferenceCount);
        Assert.Equal(0, defaultActive.Value.Items[1].CharacterReferenceCount);

        Assert.Equal(archivedId, Assert.Single(archived.Value.Items).Id);
        Assert.Equal(4, all.Value.TotalCount);
        Assert.Equal(
            [archivedId, CatalogSeedIds.MarvelUniverse, CatalogSeedIds.DcUniverse,
                CatalogSeedIds.UnknownUniverse],
            all.Value.Items.Select(item => item.Id));
        Assert.Equal(archivedId, Assert.Single(displaySearch.Value.Items).Id);
        Assert.Equal(archivedId, Assert.Single(englishSearch.Value.Items).Id);
        Assert.Equal(CatalogSeedIds.DcUniverse, Assert.Single(slugSearch.Value.Items).Id);

        Assert.Equal(3, clamped.Value.TotalCount);
        Assert.Equal(2, clamped.Value.PageNumber);
        Assert.Equal(2, clamped.Value.TotalPages);
        Assert.Equal(CatalogSeedIds.UnknownUniverse, Assert.Single(clamped.Value.Items).Id);
        Assert.Equal(0, zero.Value.TotalCount);
        Assert.Equal(0, zero.Value.TotalPages);
        Assert.Equal(1, zero.Value.PageNumber);
        Assert.Empty(zero.Value.Items);
    }

    private static async Task<Guid> SeedReferencesAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);
        var brand = Brand.Create(
            Guid.Parse("82000000-0000-0000-0000-000000000001"),
            "แบรนด์จักรวาล",
            "Universe Brand",
            CatalogSlug.Create("universe-brand"),
            now,
            "test");
        var archivedId = Guid.Parse("82000000-0000-0000-0000-000000000002");
        var archived = Universe.CreateWithLogo(
            archivedId,
            "จักรวาลเก็บถาวร",
            "Archived Universe",
            CatalogSlug.Create("archived-universe"),
            CatalogMediaReference.Create(
                "universes/archived.webp",
                "/media/universes/archived.webp",
                "โลโก้จักรวาล จักรวาลเก็บถาวร"),
            now,
            "test");
        archived.Archive(now.AddMinutes(1), "test");
        dbContext.Brands.Add(brand);
        dbContext.Universes.Add(archived);
        dbContext.Characters.AddRange(
            Character.Create(
                Guid.Parse("82000000-0000-0000-0000-000000000011"),
                CatalogSeedIds.MarvelUniverse,
                "Spider-Man"),
            Character.Create(
                Guid.Parse("82000000-0000-0000-0000-000000000012"),
                CatalogSeedIds.MarvelUniverse,
                "Iron Man"));
        dbContext.Products.AddRange(
            Product(1, brand.Id, CatalogSeedIds.MarvelUniverse, now),
            Product(2, brand.Id, CatalogSeedIds.MarvelUniverse, now));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return archivedId;
    }

    private static Product Product(
        int sequence,
        Guid brandId,
        Guid universeId,
        DateTimeOffset createdAtUtc) =>
        ToyStore.Domain.Products.Product.CreateInStock(
            Guid.Parse($"82000000-0000-0000-0000-{sequence:000000000000}"),
            $"สินค้าอ้างอิง {sequence}",
            $"Reference Product {sequence}",
            "รายละเอียด",
            $"reference-product-{sequence}",
            CatalogSeedIds.ArtToyCategory,
            brandId,
            universeId,
            InStockOffer.Create(Money.Create(100)),
            createdAtUtc,
            "test");
}
