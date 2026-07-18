using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgresPersistenceFailureClassifierTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task RealNormalizedBrandConstraintMapsByExactDatabaseName()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Brands.Add(Brand.Create(
            Guid.NewGuid(),
            "ชื่อซ้ำ",
            "Original English",
            CatalogSlug.Create("original-english"),
            UtcNow,
            "test"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        dbContext.Brands.Add(Brand.Create(
            Guid.NewGuid(),
            "  ชื่อซ้ำ  ",
            "Different English",
            CatalogSlug.Create("different-english"),
            UtcNow,
            "test"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        var classifier = scope.ServiceProvider.GetRequiredService<IPersistenceFailureClassifier>();

        Assert.Equal(
            new PersistenceFailure(
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateDisplayName),
            classifier.Classify(exception));
    }

    [Fact]
    public async Task RealScopedCharacterConstraintMapsByExactDatabaseName()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Characters.Add(Character.Create(
            Guid.NewGuid(),
            CatalogSeedIds.MarvelUniverse,
            "Spider Man"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        dbContext.Characters.Add(Character.Create(
            Guid.NewGuid(),
            CatalogSeedIds.MarvelUniverse,
            " ＳＰＩＤＥＲ   ＭＡＮ "));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        var classifier = scope.ServiceProvider.GetRequiredService<IPersistenceFailureClassifier>();

        Assert.Equal(
            new PersistenceFailure(
                PersistenceFailureTarget.Character,
                PersistenceFailureKind.DuplicateName),
            classifier.Classify(exception));
    }

    [Fact]
    public async Task RealOptimisticConcurrencyFailureMapsWithoutProviderLeakage()
    {
        await using var factory = await StartAndResetAsync();
        var brandId = Guid.NewGuid();
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedContext.Brands.Add(Brand.Create(
                brandId,
                "แบรนด์เริ่มต้น",
                "Initial Brand",
                CatalogSlug.Create("initial-brand"),
                UtcNow,
                "test"));
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = await firstContext.Brands.SingleAsync(
            brand => brand.Id == brandId,
            TestContext.Current.CancellationToken);
        var second = await secondContext.Brands.SingleAsync(
            brand => brand.Id == brandId,
            TestContext.Current.CancellationToken);
        first.UpdateDetails(
            "แบรนด์รุ่นหนึ่ง",
            "First Revision",
            CatalogSlug.Create("first-revision"),
            UtcNow.AddMinutes(1),
            "first");
        second.UpdateDetails(
            "แบรนด์รุ่นสอง",
            "Second Revision",
            CatalogSlug.Create("second-revision"),
            UtcNow.AddMinutes(1),
            "second");
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        var classifier = secondScope.ServiceProvider.GetRequiredService<IPersistenceFailureClassifier>();

        Assert.Equal(
            new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict),
            classifier.Classify(exception));
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }
}
