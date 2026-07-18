using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogSlugAllocatorTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task AllocationRequiresCallerOwnedActiveTransaction()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allocator = new CatalogSlugAllocator(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            allocator.AllocateBrandAsync("Toy", TestContext.Current.CancellationToken));

        Assert.Contains("active database transaction", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllocationUsesBaseLowestGapPunctuationCollisionAndRollbackReuse()
    {
        await using var factory = await StartAndResetAsync();
        Assert.Equal("toy", await InsertBrandAsync(factory, "Toy", "Toy", commit: true));
        Assert.Equal("toy-2", await InsertBrandAsync(factory, "Toy!!!", "Toy Two", commit: true));
        Assert.Equal("toy-3", await InsertBrandAsync(factory, "Toy???", "Toy Three", commit: true));

        await DeleteBrandBySlugAsync(factory, "toy-2");
        Assert.Equal("toy-2", await InsertBrandAsync(factory, "Toy---", "Toy Gap", commit: true));

        var rolledBack = await InsertBrandAsync(factory, "Rollback", "Rollback First", commit: false);
        var reused = await InsertBrandAsync(factory, "Rollback", "Rollback Second", commit: true);
        Assert.Equal("rollback", rolledBack);
        Assert.Equal("rollback", reused);
    }

    [Fact]
    public async Task SameBaseConcurrentAllocationsCommitDistinctDeterministicSlugs()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;

        async Task<string> InsertAsync(string englishName, string displayName)
        {
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await InsertBrandAsync(factory, englishName, displayName, commit: true);
        }

        var slugs = await Task.WhenAll(
            InsertAsync("Concurrent Toy", "Concurrent One"),
            InsertAsync("Concurrent---Toy", "Concurrent Two"));

        Assert.Equal(["concurrent-toy", "concurrent-toy-2"], slugs.Order());
    }

    [Fact]
    public async Task ScopeLockSerializesOverlappingToyAndToy2Bases()
    {
        await using var factory = await StartAndResetAsync();
        Assert.Equal("toy", await InsertBrandAsync(factory, "Toy", "Existing Toy", commit: true));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;

        async Task<string> InsertAsync(string englishName, string displayName)
        {
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await InsertBrandAsync(factory, englishName, displayName, commit: true);
        }

        var slugs = await Task.WhenAll(
            InsertAsync("Toy!", "Overlapping One"),
            InsertAsync("Toy 2", "Overlapping Two"));

        var ordered = slugs.Order().ToArray();
        Assert.True(
            ordered.SequenceEqual(["toy-2", "toy-2-2"])
            || ordered.SequenceEqual(["toy-2", "toy-3"]),
            $"Unexpected overlapping allocation result: {string.Join(", ", ordered)}");
    }

    [Fact]
    public async Task BrandAndUniverseScopesAllocateSameSlugInParallel()
    {
        await using var factory = await StartAndResetAsync();
        var brandTask = InsertBrandAsync(factory, "Shared Scope", "Brand Shared", commit: true);
        var universeTask = InsertUniverseAsync(factory, "Shared Scope", "Universe Shared");

        var slugs = await Task.WhenAll(brandTask, universeTask);

        Assert.Equal(["shared-scope", "shared-scope"], slugs);
    }

    [Fact]
    public async Task ProductAllocationUsesBaseLowestGapAndRollbackReuse()
    {
        await using var factory = await StartAndResetAsync();
        var brandId = await CreateProductBrandAsync(factory, "product-gap");
        Assert.Equal("product-toy", await InsertProductAsync(factory, brandId, "Product Toy", "Product One", commit: true));
        Assert.Equal("product-toy-2", await InsertProductAsync(factory, brandId, "Product---Toy", "Product Two", commit: true));
        Assert.Equal("product-toy-3", await InsertProductAsync(factory, brandId, "Product???Toy", "Product Three", commit: true));
        await DeleteProductBySlugAsync(factory, "product-toy-2");
        Assert.Equal("product-toy-2", await InsertProductAsync(factory, brandId, "Product___Toy", "Product Gap", commit: true));

        var rolledBack = await InsertProductAsync(factory, brandId, "Rollback Product", "Rollback One", commit: false);
        var reused = await InsertProductAsync(factory, brandId, "Rollback Product", "Rollback Two", commit: true);
        Assert.Equal("rollback-product", rolledBack);
        Assert.Equal("rollback-product", reused);
    }

    [Fact]
    public async Task ProductSameBaseConcurrentAllocationsCommitDistinctSlugs()
    {
        await using var factory = await StartAndResetAsync();
        var brandId = await CreateProductBrandAsync(factory, "product-concurrent");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;

        async Task<string> InsertAsync(string englishName, string displayName)
        {
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await InsertProductAsync(factory, brandId, englishName, displayName, commit: true);
        }

        var slugs = await Task.WhenAll(
            InsertAsync("Concurrent Product", "Product Concurrent One"),
            InsertAsync("Concurrent---Product", "Product Concurrent Two"));
        Assert.Equal(["concurrent-product", "concurrent-product-2"], slugs.Order());
    }

    [Fact]
    public async Task ProductScopeLockSerializesOverlappingBases()
    {
        await using var factory = await StartAndResetAsync();
        var brandId = await CreateProductBrandAsync(factory, "product-overlap");
        Assert.Equal("toy", await InsertProductAsync(factory, brandId, "Toy", "Existing Product Toy", commit: true));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;

        async Task<string> InsertAsync(string englishName, string displayName)
        {
            if (Interlocked.Increment(ref participants) == 2)
            {
                ready.SetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await InsertProductAsync(factory, brandId, englishName, displayName, commit: true);
        }

        var slugs = (await Task.WhenAll(
                InsertAsync("Toy!", "Overlapping Product One"),
                InsertAsync("Toy 2", "Overlapping Product Two")))
            .Order()
            .ToArray();
        Assert.True(
            slugs.SequenceEqual(["toy-2", "toy-2-2"])
            || slugs.SequenceEqual(["toy-2", "toy-3"]),
            $"Unexpected Product overlap allocation: {string.Join(", ", slugs)}");
    }

    [Fact]
    public async Task ProductAndBrandScopesAllocateSameSlugIndependentlyInParallel()
    {
        await using var factory = await StartAndResetAsync();
        var brandId = await CreateProductBrandAsync(factory, "product-independent");
        var slugs = await Task.WhenAll(
            InsertProductAsync(factory, brandId, "Independent Scope", "Independent Product", commit: true),
            InsertBrandAsync(factory, "Independent Scope", "Independent Brand", commit: true));
        Assert.Equal(["independent-scope", "independent-scope"], slugs);
    }

    private static async Task<string> InsertBrandAsync(
        ToyStoreWebApplicationFactory factory,
        string englishName,
        string displayName,
        bool commit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allocator = new CatalogSlugAllocator(db);
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var slug = await allocator.AllocateBrandAsync(englishName, TestContext.Current.CancellationToken);
        db.Brands.Add(Brand.Create(
            Guid.NewGuid(), displayName, englishName, slug,
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero), "test"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        return slug.Value;
    }

    private static async Task<string> InsertUniverseAsync(
        ToyStoreWebApplicationFactory factory,
        string englishName,
        string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allocator = new CatalogSlugAllocator(db);
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var slug = await allocator.AllocateUniverseAsync(englishName, TestContext.Current.CancellationToken);
        db.Universes.Add(Universe.Create(
            Guid.NewGuid(), displayName, englishName, slug,
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero), "test"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        return slug.Value;
    }

    private static async Task<string> InsertProductAsync(
        ToyStoreWebApplicationFactory factory,
        Guid brandId,
        string englishName,
        string displayName,
        bool commit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allocator = new CatalogSlugAllocator(db);
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var slug = await allocator.AllocateProductAsync(englishName, TestContext.Current.CancellationToken);
        db.Products.Add(Product.CreateInStock(
            Guid.NewGuid(), displayName, englishName, "Description", slug.Value,
            CatalogSeedIds.GundamCategory, brandId, CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            "test"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        return slug.Value;
    }

    private static async Task<Guid> CreateProductBrandAsync(
        ToyStoreWebApplicationFactory factory,
        string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(
            Guid.NewGuid(), $"Product Brand {key}", $"Product Brand {key}", CatalogSlug.Create($"product-brand-{key}"),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero), "test");
        db.Brands.Add(brand);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return brand.Id;
    }

    private static async Task DeleteProductBySlugAsync(
        ToyStoreWebApplicationFactory factory,
        string slug)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.SingleAsync(
            value => value.Slug == slug,
            TestContext.Current.CancellationToken);
        db.Products.Remove(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task DeleteBrandBySlugAsync(
        ToyStoreWebApplicationFactory factory,
        string slug)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = (await db.Brands.ToListAsync(TestContext.Current.CancellationToken))
            .Single(value => value.Slug.Value == slug);
        db.Brands.Remove(brand);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }
}
