using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Brands;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogReferenceMutationLockTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ConcurrentNormalizedBrandDuplicatesYieldOneSuccessAndOneTypedFailure()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<Guid>> CreateAsync(string displayName, string englishName)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var sessionFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            var candidate = Brand.Create(
                Guid.NewGuid(), displayName, englishName, CatalogSlug.Create("temporary"),
                UtcNow, "test");

            var execution = await session.ExecuteOnceAsync(
                async cancellationToken =>
                {
                    if (Interlocked.Increment(ref entered) == 2)
                    {
                        ready.TrySetResult();
                    }

                    await ready.Task.WaitAsync(cancellationToken);
                    await session.AcquireMutationLockAsync(cancellationToken);
                    if (await session.DisplayNameExistsAsync(
                            candidate.NormalizedDisplayName,
                            null,
                            cancellationToken))
                    {
                        return Result<Guid>.Failure(BrandErrors.DuplicateDisplayName);
                    }

                    if (await session.EnglishNameExistsAsync(
                            candidate.NormalizedEnglishName,
                            null,
                            cancellationToken))
                    {
                        return Result<Guid>.Failure(BrandErrors.DuplicateEnglishName);
                    }

                    var slug = await session.AllocateSlugAsync(
                        englishName,
                        null,
                        cancellationToken);
                    var brand = Brand.Create(
                        Guid.NewGuid(), displayName, englishName, slug, UtcNow, "test");
                    session.Add(brand);
                    return Result<Guid>.Success(brand.Id);
                },
                TestContext.Current.CancellationToken);

            return execution.Result;
        }

        var results = await Task.WhenAll(
            CreateAsync("  แบรนด์ซ้ำ  ", "First English Name"),
            CreateAsync("แบรนด์ซ้ำ", "Second English Name"));

        Assert.Single(results, result => result.IsSuccess);
        var failure = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(BrandErrors.DuplicateDisplayName, failure.Error);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expectedNormalizedName = Brand.Create(
            Guid.NewGuid(),
            "แบรนด์ซ้ำ",
            "Normalization Probe",
            CatalogSlug.Create("normalization-probe"),
            UtcNow,
            "test").NormalizedDisplayName;
        Assert.Equal(1, await dbContext.Brands.CountAsync(
            brand => brand.NormalizedDisplayName == expectedNormalizedName,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LockedConcurrentSlugCollisionsAllocateDeterministicSuffixes()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<string> CreateAsync(string displayName, string englishName)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var sessionFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            var execution = await session.ExecuteOnceAsync(
                async cancellationToken =>
                {
                    if (Interlocked.Increment(ref entered) == 2)
                    {
                        ready.TrySetResult();
                    }

                    await ready.Task.WaitAsync(cancellationToken);
                    await session.AcquireMutationLockAsync(cancellationToken);
                    var slug = await session.AllocateSlugAsync(
                        englishName,
                        null,
                        cancellationToken);
                    session.Add(Brand.Create(
                        Guid.NewGuid(), displayName, englishName, slug, UtcNow, "test"));
                    return Result<string>.Success(slug.Value);
                },
                TestContext.Current.CancellationToken);
            return execution.Result.Value;
        }

        var slugs = await Task.WhenAll(
            CreateAsync("แบรนด์หนึ่ง", "Same Slug"),
            CreateAsync("แบรนด์สอง", "Same---Slug"));

        Assert.Equal(["same-slug", "same-slug-2"], slugs.Order());
    }

    [Fact]
    public async Task ConcurrentNormalizedUniverseDuplicatesYieldOneSuccessAndOneTypedFailure()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<Guid>> CreateAsync(string displayName, string englishName)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var sessionFactory = scope.ServiceProvider.GetRequiredService<IUniverseMutationSessionFactory>();
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            var candidate = Universe.Create(
                Guid.NewGuid(), displayName, englishName, CatalogSlug.Create("temporary"),
                UtcNow, "test");
            var execution = await session.ExecuteOnceAsync(
                async cancellationToken =>
                {
                    if (Interlocked.Increment(ref entered) == 2)
                    {
                        ready.TrySetResult();
                    }

                    await ready.Task.WaitAsync(cancellationToken);
                    await session.AcquireMutationLockAsync(cancellationToken);
                    if (await session.DisplayNameExistsAsync(
                            candidate.NormalizedDisplayName,
                            null,
                            cancellationToken))
                    {
                        return Result<Guid>.Failure(UniverseErrors.DuplicateDisplayName);
                    }

                    var slug = await session.AllocateSlugAsync(
                        englishName,
                        null,
                        cancellationToken);
                    var universe = Universe.Create(
                        Guid.NewGuid(), displayName, englishName, slug, UtcNow, "test");
                    session.Add(universe);
                    return Result<Guid>.Success(universe.Id);
                },
                TestContext.Current.CancellationToken);
            return execution.Result;
        }

        var results = await Task.WhenAll(
            CreateAsync("จักรวาลใหม่", "First Universe"),
            CreateAsync("  จักรวาลใหม่  ", "Second Universe"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            UniverseErrors.DuplicateDisplayName,
            Assert.Single(results, result => result.IsFailure).Error);
    }

    [Fact]
    public async Task ArchivedNamesAndSlugsStayReservedWhileEditExcludesCurrentId()
    {
        await using var factory = await StartAndResetAsync();
        var archivedId = Guid.NewGuid();
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var archived = Brand.Create(
                archivedId,
                "แบรนด์ที่เก็บถาวร",
                "Reserved Brand",
                CatalogSlug.Create("reserved-brand"),
                UtcNow,
                "test");
            archived.Archive(UtcNow.AddMinutes(1), "test");
            dbContext.Brands.Add(archived);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var sessionFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async cancellationToken =>
            {
                await session.AcquireMutationLockAsync(cancellationToken);
                var archived = await session.FindAsync(archivedId, cancellationToken);
                Assert.NotNull(archived);
                Assert.True(await session.DisplayNameExistsAsync(
                    archived.NormalizedDisplayName,
                    null,
                    cancellationToken));
                Assert.True(await session.EnglishNameExistsAsync(
                    archived.NormalizedEnglishName,
                    null,
                    cancellationToken));

                var reservedSlug = await session.AllocateSlugAsync(
                    archived.EnglishName,
                    null,
                    cancellationToken);
                var currentSlug = await session.AllocateSlugAsync(
                    archived.EnglishName,
                    archived.Id,
                    cancellationToken);

                return Result<(string Reserved, string Current)>.Success(
                    (reservedSlug.Value, currentSlug.Value));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("reserved-brand-2", execution.Result.Value.Reserved);
        Assert.Equal("reserved-brand", execution.Result.Value.Current);
    }

    [Fact]
    public async Task EnglishEditReallocatesSlugAndVersionInsideOneLockedSession()
    {
        await using var factory = await StartAndResetAsync();
        var editedId = Guid.NewGuid();
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Brands.AddRange(
                Brand.CreateWithImage(
                    editedId,
                    "แบรนด์เดิม",
                    "Original Brand",
                    CatalogSlug.Create("original-brand"),
                    Media("edited"),
                    UtcNow,
                    "test"),
                Brand.Create(
                    Guid.NewGuid(),
                    "แบรนด์ที่จอง slug",
                    "New Name!",
                    CatalogSlug.Create("new-name"),
                    UtcNow,
                    "test"));
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var sessionFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async cancellationToken =>
            {
                await session.AcquireMutationLockAsync(cancellationToken);
                var brand = Assert.IsType<Brand>(
                    await session.FindAsync(editedId, cancellationToken));
                var slug = await session.AllocateSlugAsync(
                    "New---Name",
                    brand.Id,
                    cancellationToken);
                brand.UpdateDetailsWithImage(
                    "แบรนด์ใหม่",
                    "New---Name",
                    slug,
                    replacementImage: null,
                    expectedVersion: brand.Version,
                    UtcNow.AddMinutes(1),
                    "editor");
                return Result<Guid>.Success(brand.Id);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.Committed, execution.CommitOutcome);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var reloaded = await verificationContext.Brands.SingleAsync(
            brand => brand.Id == editedId,
            TestContext.Current.CancellationToken);
        Assert.Equal("new-name-2", reloaded.Slug.Value);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal("New---Name", reloaded.EnglishName);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static CatalogMediaReference Media(string key) =>
        CatalogMediaReference.Create(
            $"brands/{key}.webp",
            $"/media/brands/{key}.webp",
            $"รูปแบรนด์ {key}");

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }
}
