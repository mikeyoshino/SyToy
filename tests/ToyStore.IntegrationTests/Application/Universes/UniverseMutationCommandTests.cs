using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ArchiveUniverse;
using ToyStore.Application.Universes.CreateUniverse;
using ToyStore.Application.Universes.UpdateUniverse;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Universes;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class UniverseMutationCommandTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task LogoLessSeedRequiresSelectionThenPersistsTrustedThaiLogo()
    {
        await using var factory = await StartAndResetAsync();

        var missing = await UpdateAsync(
            factory,
            new UpdateUniverseCommand(
                CatalogSeedIds.MarvelUniverse,
                1,
                "มาร์เวล",
                "Marvel",
                ReplacementLogo: null));

        Assert.Equal(UniverseErrors.MissingMedia, missing.Error);

        var updated = await UpdateAsync(
            factory,
            new UpdateUniverseCommand(
                CatalogSeedIds.MarvelUniverse,
                1,
                "มาร์เวลฉบับสะสม",
                "Marvel Collectibles",
                Upload()));

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value.Version);
        var noOp = await UpdateAsync(
            factory,
            new UpdateUniverseCommand(
                CatalogSeedIds.MarvelUniverse,
                updated.Value.Version,
                " มาร์เวลฉบับสะสม ",
                " Marvel Collectibles ",
                ReplacementLogo: null));
        Assert.True(noOp.IsSuccess);
        Assert.Equal(updated.Value.Version, noOp.Value.Version);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.Universes.AsNoTracking().SingleAsync(
            universe => universe.Id == CatalogSeedIds.MarvelUniverse,
            TestContext.Current.CancellationToken);
        Assert.Equal("marvel-collectibles", persisted.Slug.Value);
        Assert.Equal("โลโก้จักรวาล มาร์เวลฉบับสะสม", persisted.Logo!.AltText);
        Assert.True(persisted.CanBeUsedByPublishedProduct);
        Assert.Single(CommittedFiles(factory));
    }

    [Fact]
    public async Task ConcurrentDuplicateCreatesReturnOneTypedFailureAndOneCommittedFile()
    {
        await using var factory = await StartAndResetAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<UniverseMutationResult>> CreateAsync(string englishName)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await ExecuteCreateAsync(
                factory,
                new CreateUniverseCommand("จักรวาลชนกัน", englishName, Upload()));
        }

        var results = await Task.WhenAll(
            CreateAsync("Collision Universe One"),
            CreateAsync("Collision Universe Two"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            UniverseErrors.DuplicateDisplayName,
            Assert.Single(results, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Universes.CountAsync(
            universe => universe.NormalizedDisplayName
                == CatalogNameNormalizer.Normalize("จักรวาลชนกัน"),
            TestContext.Current.CancellationToken));
        Assert.Single(CommittedFiles(factory));
    }

    [Fact]
    public async Task ConcurrentReplacementUpdatesReturnOneStaleAndKeepOnlyWinningLogo()
    {
        await using var factory = await StartAndResetAsync();
        var created = await ExecuteCreateAsync(
            factory,
            new CreateUniverseCommand("จักรวาลแข่งขัน", "Race Universe", Upload()));
        Assert.True(created.IsSuccess);
        var id = created.Value.Id;
        var originalFile = Assert.Single(CommittedFiles(factory));

        var updates = await Task.WhenAll(
            UpdateAsync(factory, new UpdateUniverseCommand(
                id, 1, "จักรวาลรุ่นหนึ่ง", "Race Universe One", Upload())),
            UpdateAsync(factory, new UpdateUniverseCommand(
                id, 1, "จักรวาลรุ่นสอง", "Race Universe Two", Upload())));

        Assert.Single(updates, result => result.IsSuccess);
        Assert.Equal(
            UniverseErrors.StaleVersion,
            Assert.Single(updates, result => result.IsFailure).Error);
        Assert.False(File.Exists(originalFile));
        Assert.Single(CommittedFiles(factory));
        Assert.Empty(StagingFiles(factory));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.Universes.AsNoTracking().SingleAsync(
            universe => universe.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, persisted.Version);
        Assert.True(File.Exists(CommittedPath(factory, persisted.Logo!.StorageKey)));
    }

    [Fact]
    public async Task ReplacementUpdateArchiveRaceKeepsReferencedWinnerLogoAndLeaksNoStaging()
    {
        await using var factory = await StartAndResetAsync();
        var created = await ExecuteCreateAsync(
            factory,
            new CreateUniverseCommand(
                "จักรวาลอัปเดตเก็บถาวร",
                "Update Archive Universe",
                Upload()));
        Assert.True(created.IsSuccess);

        var race = await Task.WhenAll(
            UpdateAsync(factory, new UpdateUniverseCommand(
                created.Value.Id,
                created.Value.Version,
                "จักรวาลที่แก้แล้ว",
                "Updated Archive Universe",
                Upload())),
            ArchiveAsync(factory, new ArchiveUniverseCommand(
                created.Value.Id,
                created.Value.Version)));

        Assert.Single(race, result => result.IsSuccess);
        var failure = Assert.Single(race, result => result.IsFailure);
        Assert.Contains(
            failure.Error,
            new[] { UniverseErrors.StaleVersion, UniverseErrors.Archived });
        Assert.Single(CommittedFiles(factory));
        Assert.Empty(StagingFiles(factory));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.Universes.AsNoTracking().SingleAsync(
            universe => universe.Id == created.Value.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, persisted.Version);
        Assert.True(File.Exists(CommittedPath(factory, persisted.Logo!.StorageKey)));
    }

    [Fact]
    public async Task ReplacementDeletesOldLogoAndArchivePreservesProductCharacterAndLogoReferences()
    {
        await using var factory = await StartAndResetAsync();
        var created = await ExecuteCreateAsync(
            factory,
            new CreateUniverseCommand("จักรวาลอ้างอิง", "Referenced Universe", Upload()));
        Assert.True(created.IsSuccess);
        var originalFile = Assert.Single(CommittedFiles(factory));

        var replacement = await UpdateAsync(
            factory,
            new UpdateUniverseCommand(
                created.Value.Id,
                created.Value.Version,
                "จักรวาลอ้างอิงใหม่",
                "Referenced Universe Updated",
                Upload()));
        Assert.True(replacement.IsSuccess);
        Assert.False(File.Exists(originalFile));
        Assert.Single(CommittedFiles(factory));

        await SeedReferencesAsync(factory, created.Value.Id);
        var archived = await ArchiveAsync(
            factory,
            new ArchiveUniverseCommand(created.Value.Id, replacement.Value.Version));

        Assert.True(archived.IsSuccess);
        Assert.Equal(CatalogReferenceStatus.Archived, archived.Value.Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await dbContext.Universes.AsNoTracking().SingleAsync(
            universe => universe.Id == created.Value.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.Logo);
        Assert.Equal(1, await dbContext.Products.CountAsync(
            product => product.UniverseId == persisted.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await dbContext.Characters.CountAsync(
            character => character.UniverseId == persisted.Id,
            TestContext.Current.CancellationToken));
        Assert.Single(CommittedFiles(factory));
    }

    private static async Task<Result<UniverseMutationResult>> ExecuteCreateAsync(
        ToyStoreWebApplicationFactory factory,
        CreateUniverseCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateUniverseCommand, Result<UniverseMutationResult>>>();
        var authorization = new AuthorizationBehavior<
            CreateUniverseCommand,
            Result<UniverseMutationResult>>(new AdminAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Result<UniverseMutationResult>> UpdateAsync(
        ToyStoreWebApplicationFactory factory,
        UpdateUniverseCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<UpdateUniverseCommand, Result<UniverseMutationResult>>>();
        var authorization = new AuthorizationBehavior<
            UpdateUniverseCommand,
            Result<UniverseMutationResult>>(new AdminAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Result<UniverseMutationResult>> ArchiveAsync(
        ToyStoreWebApplicationFactory factory,
        ArchiveUniverseCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<ArchiveUniverseCommand, Result<UniverseMutationResult>>>();
        var authorization = new AuthorizationBehavior<
            ArchiveUniverseCommand,
            Result<UniverseMutationResult>>(new AdminAuthorization());
        return await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private static async Task SeedReferencesAsync(
        ToyStoreWebApplicationFactory factory,
        Guid universeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var brand = Brand.Create(
            Guid.NewGuid(),
            "แบรนด์อ้างอิง",
            "Reference Brand",
            CatalogSlug.Create($"reference-brand-{Guid.NewGuid():N}"),
            now,
            "test");
        dbContext.Brands.Add(brand);
        dbContext.Characters.Add(Character.Create(
            Guid.NewGuid(), universeId, "ตัวละครอ้างอิง"));
        dbContext.Products.Add(Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้าอ้างอิง",
            "Reference Product",
            "รายละเอียด",
            $"reference-product-{Guid.NewGuid():N}",
            CatalogSeedIds.ArtToyCategory,
            brand.Id,
            universeId,
            InStockOffer.Create(Money.Create(100)),
            now,
            "test"));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff, 1, 2, 3]), "image/jpeg");

    private static string[] CommittedFiles(ToyStoreWebApplicationFactory factory)
    {
        var root = Path.Combine(factory.StorageRootPath, "files");
        return Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".jpg" or ".png" or ".webp")
                .ToArray()
            : [];
    }

    private static string[] StagingFiles(ToyStoreWebApplicationFactory factory)
    {
        var root = Path.Combine(factory.StorageRootPath, ".staging");
        return Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            : [];
    }

    private static string CommittedPath(
        ToyStoreWebApplicationFactory factory,
        string storageKey) =>
        Path.Combine(
            factory.StorageRootPath,
            "files",
            storageKey.Replace('/', Path.DirectorySeparatorChar));

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private sealed class AdminAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, "integration-admin"));
    }
}
