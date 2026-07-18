using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogPersistenceTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task TrackedSaveClearReloadRoundTripsInStockAggregateAndRelations()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brand = Brand.Create(Guid.NewGuid(), "บันได", "Bandai", CatalogSlug.Create("bandai"), now, "test");
        brand.AttachOrReplaceImage(Media("bandai"), now, "test");
        var character = Character.Create(Guid.NewGuid(), CatalogSeedIds.UnknownUniverse, "RX-78-2");
        var product = Product.CreateInStock(
            Guid.NewGuid(),
            "  กันดั้ม　 RX-78-2 ",
            " ＧＵＮＤＡＭ   RX-78-2 ",
            "โมเดลกันดั้ม",
            "gundam-rx-78-2",
            CatalogSeedIds.GundamCategory,
            brand.Id,
            CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(1234.567890123m)),
            now,
            "test");
        product.AddImage(Guid.NewGuid(), "products/rx/main.webp", "/media/products/rx/main.webp", "ภาพหลัก", now, "test");
        product.AddImage(Guid.NewGuid(), "products/rx/back.webp", "/media/products/rx/back.webp", "ภาพด้านหลัง", now, "test");
        product.AddCharacter(character.Id, now, "test");

        db.Brands.Add(brand);
        db.Characters.Add(character);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.Products
            .Include(current => current.Images)
            .Include(current => current.Characters)
            .SingleAsync(current => current.Id == product.Id, TestContext.Current.CancellationToken);

        Assert.Equal("กันดั้ม RX-78-2", reloaded.NormalizedDisplayName);
        Assert.Equal("GUNDAM RX-78-2", reloaded.NormalizedEnglishName);
        Assert.Equal(1234.567890123m, reloaded.InStockOffer!.Price.Amount);
        Assert.Null(reloaded.PreOrderOffer);
        Assert.Collection(
            reloaded.Images.OrderBy(image => image.SortOrder),
            image => Assert.True(image.IsPrimary),
            image => Assert.False(image.IsPrimary));
        Assert.Equal(character.Id, Assert.Single(reloaded.Characters).CharacterId);
        reloaded.RemoveCharacter(character.Id, now.AddSeconds(1), "test-2");
        Assert.Empty(reloaded.Characters);
    }

    [Fact]
    public async Task EfLoadedCompleteReplacementReusesRetainedChildrenAndPersistsOneReconciliation()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brand = Brand.Create(
            Guid.NewGuid(), "Reconcile Brand", "Reconcile Brand",
            CatalogSlug.Create("reconcile-brand"), now, "test");
        var retainedCharacter = Character.Create(
            Guid.NewGuid(), CatalogSeedIds.UnknownUniverse, "Retained Character");
        var removedCharacter = Character.Create(
            Guid.NewGuid(), CatalogSeedIds.UnknownUniverse, "Removed Character");
        var addedCharacter = Character.Create(
            Guid.NewGuid(), CatalogSeedIds.UnknownUniverse, "Added Character");
        var removedImage = Image(Guid.NewGuid(), "reconcile/front.webp", "front");
        var retainedImage = Image(Guid.NewGuid(), "reconcile/back.webp", "back");
        var product = Product.CreateInStock(
            Guid.NewGuid(), "Reconcile Product", "Reconcile Product", "Description",
            "reconcile-product", CatalogSeedIds.GundamCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, InStockOffer.Create(Money.Create(100)),
            [removedImage, retainedImage],
            [removedCharacter.Id, retainedCharacter.Id],
            now,
            "test");
        db.Brands.Add(brand);
        db.Characters.AddRange(retainedCharacter, removedCharacter, addedCharacter);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var loaded = await db.Products
            .Include(value => value.Images)
            .Include(value => value.Characters)
            .SingleAsync(
                value => value.Id == product.Id,
                TestContext.Current.CancellationToken);
        var retainedImageInstance = loaded.Images.Single(image => image.Id == retainedImage.Id);
        var retainedCharacterInstance = loaded.Characters.Single(
            link => link.CharacterId == retainedCharacter.Id);
        var addedImage = Image(Guid.NewGuid(), "reconcile/side.webp", "side");

        loaded.UpdateDraftInStock(
            loaded.DisplayName,
            loaded.EnglishName,
            loaded.Description,
            loaded.Slug,
            loaded.ProductCategoryId,
            loaded.BrandId,
            loaded.UniverseId,
            loaded.InStockOffer!,
            [retainedImage, addedImage],
            [retainedCharacter.Id, addedCharacter.Id],
            loaded.Version,
            now.AddMinutes(1),
            "admin-2");

        Assert.Same(retainedImageInstance, loaded.Images[0]);
        Assert.Same(
            retainedCharacterInstance,
            loaded.Characters.Single(link => link.CharacterId == retainedCharacter.Id));
        Assert.Equal(2, loaded.Version);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.Products
            .Include(value => value.Images)
            .Include(value => value.Characters)
            .SingleAsync(
                value => value.Id == product.Id,
                TestContext.Current.CancellationToken);
        Assert.Equal(
            [retainedImage.Id, addedImage.Id],
            reloaded.Images.OrderBy(image => image.SortOrder).Select(image => image.Id));
        Assert.Equal(
            new[] { retainedCharacter.Id, addedCharacter.Id }.Order(),
            reloaded.Characters.Select(link => link.CharacterId).Order());
    }

    [Fact]
    public async Task TrackedSaveClearReloadRoundTripsPreOrderOfferAndUtcEta()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brand = Brand.Create(Guid.NewGuid(), "ป๊อปมาร์ท", "Pop Mart", CatalogSlug.Create("pop-mart"), now, "test");
        var offer = PreOrderOffer.Create(
            Money.Create(9999.999999999m),
            Money.Create(1999.111111111m),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(2, 2027),
            100,
            3,
            now,
            7);
        var product = Product.CreatePreOrder(
            Guid.NewGuid(), "อาร์ตทอย", "Art Toy", "พรีออเดอร์", "art-toy", CatalogSeedIds.ArtToyCategory,
            brand.Id, CatalogSeedIds.UnknownUniverse, offer, now, "test");

        db.Brands.Add(brand);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.Products.SingleAsync(
            current => current.Id == product.Id,
            TestContext.Current.CancellationToken);

        Assert.Null(reloaded.InStockOffer);
        Assert.Equal(9999.999999999m, reloaded.PreOrderOffer!.FullPrice.Amount);
        Assert.Equal(1999.111111111m, reloaded.PreOrderOffer.DepositAmount.Amount);
        Assert.Equal(8000.888888888m, reloaded.PreOrderOffer.BalanceAmount.Amount);
        Assert.Equal(TimeSpan.Zero, reloaded.PreOrderOffer.CloseAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 16, 59, 59, TimeSpan.Zero), reloaded.PreOrderOffer.CloseAtUtc);
        Assert.Equal(2, reloaded.PreOrderOffer.EstimatedArrival.Month);
        Assert.Equal(2027, reloaded.PreOrderOffer.EstimatedArrival.Year);
        Assert.Equal(7, reloaded.PreOrderOffer.BalancePaymentDays);
    }

    [Fact]
    public async Task ResetRestoresExactCatalogSeedsWithoutDuplicates()
    {
        await using var factory = await StartAndResetAsync();
        await postgreSql.ResetAsync(factory.Services);
        await postgreSql.ResetAsync(factory.Services);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var categories = await db.ProductCategories.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken);
        var expectedCategories = ProductCategorySeeds.All.OrderBy(value => value.Id).ToArray();
        Assert.Equal(expectedCategories.Length, categories.Length);
        Assert.Equal(expectedCategories.Select(value => value.Id), categories.Select(value => value.Id));
        Assert.Equal(expectedCategories.Select(value => value.Code), categories.Select(value => value.Code));

        var universes = await db.Universes.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(cancellationToken);
        var expectedUniverses = UniverseSeeds.All.OrderBy(value => value.Id).ToArray();
        Assert.Equal(expectedUniverses.Length, universes.Length);
        for (var index = 0; index < expectedUniverses.Length; index++)
        {
            var expected = expectedUniverses[index];
            var actual = universes[index];
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.DisplayName, actual.DisplayName);
            Assert.Equal(expected.NormalizedDisplayName, actual.NormalizedDisplayName);
            Assert.Equal(expected.EnglishName, actual.EnglishName);
            Assert.Equal(expected.NormalizedEnglishName, actual.NormalizedEnglishName);
            Assert.Equal(expected.Slug, actual.Slug.Value);
            Assert.Equal(expected.Status, actual.Status);
            Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
            Assert.Equal(expected.CreatedBy, actual.CreatedBy);
            Assert.Equal(expected.CreatedAtUtc, actual.UpdatedAtUtc);
            Assert.Equal(expected.CreatedBy, actual.UpdatedBy);
            Assert.Null(actual.Logo);
            Assert.Null(actual.ArchivedAtUtc);
            Assert.Null(actual.ArchivedBy);
        }
    }

    [Fact]
    public async Task BrandAndUniverseMediaAuditArchiveAndCharacterIdentityRoundTrip()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var archived = created.AddDays(1);
        var brand = Brand.Create(Guid.NewGuid(), "แบรนด์", "Roundtrip Brand", CatalogSlug.Create("roundtrip-brand"), created, "creator");
        brand.AttachOrReplaceImage(Media("roundtrip"), created.AddHours(1), "media-editor");
        brand.Archive(archived, "archiver");
        var universe = Universe.Create(Guid.NewGuid(), "จักรวาล", "Roundtrip Universe", CatalogSlug.Create("roundtrip-universe"), created, "creator");
        universe.AttachOrReplaceLogo(
            CatalogMediaReference.Create("universes/roundtrip.webp", "/media/universes/roundtrip.webp", "โลโก้จักรวาล"),
            created.AddHours(1),
            "media-editor");
        universe.Archive(archived, "archiver");
        var character = Character.Create(Guid.NewGuid(), universe.Id, "  ＩＲＯＮ   MAN  ");

        db.Brands.Add(brand);
        db.Universes.Add(universe);
        db.Characters.Add(character);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var savedBrand = await db.Brands.SingleAsync(value => value.Id == brand.Id, TestContext.Current.CancellationToken);
        var savedUniverse = await db.Universes.SingleAsync(value => value.Id == universe.Id, TestContext.Current.CancellationToken);
        var savedCharacter = await db.Characters.SingleAsync(value => value.Id == character.Id, TestContext.Current.CancellationToken);
        Assert.Equal("brands/roundtrip.webp", savedBrand.Image!.StorageKey);
        Assert.Equal(CatalogReferenceStatus.Archived, savedBrand.Status);
        Assert.Equal(archived, savedBrand.ArchivedAtUtc);
        Assert.Equal("archiver", savedBrand.UpdatedBy);
        Assert.Equal("universes/roundtrip.webp", savedUniverse.Logo!.StorageKey);
        Assert.Equal(CatalogReferenceStatus.Archived, savedUniverse.Status);
        Assert.Equal("IRON MAN", savedCharacter.NormalizedName);
        Assert.Equal(universe.Id, savedCharacter.Identity.UniverseId);
    }

    [Fact]
    public async Task LoadedProductImageReorderAndRemovePersistWithImmediateUniqueIndexes()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brand = Brand.Create(Guid.NewGuid(), "Image Persistence", "Image Persistence", CatalogSlug.Create("image-persistence"), now, "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), "Image Product", "Image Product", "Description", "image-product",
            CatalogSeedIds.GundamCategory, brand.Id, CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)), now, "test");
        product.AddImage(Guid.NewGuid(), "images/front", "/media/front", "front", now, "test");
        product.AddImage(Guid.NewGuid(), "images/side", "/media/side", "side", now, "test");
        product.AddImage(Guid.NewGuid(), "images/back", "/media/back", "back", now, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == product.Id,
            TestContext.Current.CancellationToken);
        loaded.ReorderImages(
            loaded.Images.OrderByDescending(value => value.SortOrder).Select(value => value.Id).ToArray(),
            now.AddMinutes(1),
            "reorder");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == product.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            ["images/back", "images/side", "images/front"],
            loaded.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
        Assert.True(loaded.Images.Single(value => value.SortOrder == 0).IsPrimary);

        loaded.RemoveImage(loaded.Images.Single(value => value.SortOrder == 0).Id, now.AddMinutes(2), "remove");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == product.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            ["images/side", "images/front"],
            loaded.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
        Assert.True(loaded.Images.Single(value => value.SortOrder == 0).IsPrimary);
    }

    [Fact]
    public async Task ImageRebuildFailureRollsBackDatabaseAndRestoresTrackerShadowState()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "failure");
        db.ChangeTracker.Clear();
        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        loaded.RemoveImage(
            loaded.Images.Single(value => value.SortOrder == 0).Id,
            seeded.Now.AddMinutes(1),
            "remove");
        var duplicate = Brand.Create(
            Guid.NewGuid(), seeded.BrandDisplayName, "Different English", CatalogSlug.Create("different-english"),
            seeded.Now, "test");
        db.Brands.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        var trackedImages = db.ChangeTracker.Entries<ProductImage>().ToArray();
        Assert.Equal(3, trackedImages.Length);
        Assert.Single(trackedImages, entry => entry.State == EntityState.Deleted);
        Assert.All(trackedImages, entry =>
        {
            Assert.NotEqual(EntityState.Added, entry.State);
            Assert.NotEqual(EntityState.Detached, entry.State);
            Assert.Equal(seeded.ProductId, entry.Property<Guid>("ProductId").CurrentValue);
        });
        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verificationDb.Products.Include(value => value.Images).SingleAsync(
                value => value.Id == seeded.ProductId,
                TestContext.Current.CancellationToken);
            Assert.Equal(
                ["images/failure/front", "images/failure/side", "images/failure/back"],
                persisted.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
        }

        db.Entry(duplicate).State = EntityState.Detached;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var retried = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            ["images/failure/side", "images/failure/back"],
            retried.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
    }

    [Fact]
    public async Task ImageRebuildRefusesPartialNavigationBeforeDeletingRows()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "partial");
        db.ChangeTracker.Clear();
        _ = await db.Products.SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        var oneImage = await db.ProductImages.SingleAsync(
            value => EF.Property<Guid>(value, "ProductId") == seeded.ProductId && value.SortOrder == 0,
            TestContext.Current.CancellationToken);
        db.Entry(oneImage).Property(value => value.SortOrder).CurrentValue = 4;
        db.Entry(oneImage).Property(value => value.IsPrimary).CurrentValue = false;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("fully loaded", exception.Message, StringComparison.Ordinal);
        Assert.Equal(seeded.ProductId, db.Entry(oneImage).Property<Guid>("ProductId").CurrentValue);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            3,
            await verificationDb.ProductImages.CountAsync(
                value => EF.Property<Guid>(value, "ProductId") == seeded.ProductId,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ImageRebuildRefusesAddedImpostorsForOmittedPersistedImages()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "impostor");
        db.ChangeTracker.Clear();
        var persistedDefinitions = await db.ProductImages
            .AsNoTracking()
            .Where(image => EF.Property<Guid>(image, "ProductId") == seeded.ProductId)
            .OrderBy(image => image.SortOrder)
            .Select(image => new ProductImageDefinition(
                image.Id,
                image.StorageKey,
                image.PublicRelativeUrl,
                image.AltText))
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var loaded = await db.Products.SingleAsync(
            product => product.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        var retained = await db.ProductImages.SingleAsync(
            image => image.Id == persistedDefinitions[0].Id,
            TestContext.Current.CancellationToken);

        loaded.UpdateDraftInStock(
            loaded.DisplayName,
            loaded.EnglishName,
            loaded.Description,
            loaded.Slug,
            loaded.ProductCategoryId,
            loaded.BrandId,
            loaded.UniverseId,
            loaded.InStockOffer!,
            persistedDefinitions.Reverse().ToArray(),
            [],
            loaded.Version,
            seeded.Now.AddMinutes(1),
            "impostor");
        db.ChangeTracker.DetectChanges();
        foreach (var impostor in loaded.Images.Where(image => image.Id != retained.Id))
        {
            db.Entry(impostor).State = EntityState.Added;
        }

        db.Entry(loaded).Collection(product => product.Images).IsLoaded = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("partial aggregate snapshot", exception.Message, StringComparison.Ordinal);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            persistedDefinitions.Select(image => image.Id).Order(),
            await verificationDb.ProductImages
                .Where(image => EF.Property<Guid>(image, "ProductId") == seeded.ProductId)
                .Select(image => image.Id)
                .Order()
                .ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ImageRebuildParticipatesInCallerTransactionRollback()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "caller");
        db.ChangeTracker.Clear();
        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            loaded.ReorderImages(
                loaded.Images.OrderByDescending(value => value.SortOrder).Select(value => value.Id).ToArray(),
                seeded.Now.AddMinutes(1),
                "reorder");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verificationDb.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            ["images/caller/front", "images/caller/side", "images/caller/back"],
            persisted.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
    }

    [Fact]
    public async Task ImageRebuildPreservesOriginalStatesWhenAcceptAllChangesIsFalse()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "accept-false");
        db.ChangeTracker.Clear();
        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        loaded.RemoveImage(
            loaded.Images.Single(value => value.SortOrder == 0).Id,
            seeded.Now.AddMinutes(1),
            "remove");

        await db.SaveChangesAsync(
            acceptAllChangesOnSuccess: false,
            TestContext.Current.CancellationToken);

        var imageEntries = db.ChangeTracker.Entries<ProductImage>().ToArray();
        Assert.Equal(3, imageEntries.Length);
        Assert.Single(imageEntries, entry => entry.State == EntityState.Deleted);
        Assert.Equal(2, imageEntries.Count(entry => entry.State == EntityState.Modified));
        Assert.All(imageEntries, entry =>
            Assert.Equal(seeded.ProductId, entry.Property<Guid>("ProductId").CurrentValue));
        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verificationDb.Products.Include(value => value.Images).SingleAsync(
                value => value.Id == seeded.ProductId,
                TestContext.Current.CancellationToken);
            Assert.Equal(
                ["images/accept-false/side", "images/accept-false/back"],
                persisted.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
        }

        db.ChangeTracker.AcceptAllChanges();
        Assert.Equal(2, db.ChangeTracker.Entries<ProductImage>().Count());
        Assert.All(
            db.ChangeTracker.Entries<ProductImage>(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public async Task SynchronousSaveRejectsImageOrderMutationWithAsyncGuidance()
    {
        await using var factory = await StartAndResetAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await SeedImageProductAsync(db, "sync");
        db.ChangeTracker.Clear();
        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        loaded.ReorderImages(
            loaded.Images.OrderByDescending(value => value.SortOrder).Select(value => value.Id).ToArray(),
            seeded.Now.AddMinutes(1),
            "sync");

        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("SaveChangesAsync", exception.Message, StringComparison.Ordinal);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verificationDb.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == seeded.ProductId,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            ["images/sync/front", "images/sync/side", "images/sync/back"],
            persisted.Images.OrderBy(value => value.SortOrder).Select(value => value.StorageKey));
    }

    [Fact]
    public async Task RollbackFailureStillRestoresTrackerAndPreservesBothExceptions()
    {
        await using var factory = await StartAndResetAsync();
        Guid productId;
        string brandDisplayName;
        DateTimeOffset now;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeded = await SeedImageProductAsync(seedDb, "rollback-failure");
            productId = seeded.ProductId;
            brandDisplayName = seeded.BrandDisplayName;
            now = seeded.Now;
        }

        var interceptor = new RollbackFailureInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new ApplicationDbContext(options);
        var loaded = await db.Products.Include(value => value.Images).SingleAsync(
            value => value.Id == productId,
            TestContext.Current.CancellationToken);
        loaded.RemoveImage(
            loaded.Images.Single(value => value.SortOrder == 0).Id,
            now.AddMinutes(1),
            "remove");
        db.Brands.Add(Brand.Create(
            Guid.NewGuid(), brandDisplayName, "Rollback Failure Duplicate", CatalogSlug.Create("rollback-failure-duplicate"),
            now, "test"));
        using var callerCancellation = new CancellationTokenSource();

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            db.SaveChangesAsync(callerCancellation.Token));

        Assert.Contains(exception.InnerExceptions, value => value is DbUpdateException);
        Assert.Contains(exception.InnerExceptions, value => value is InjectedRollbackException);
        Assert.False(interceptor.RollbackToken.CanBeCanceled);
        var imageEntries = db.ChangeTracker.Entries<ProductImage>().ToArray();
        Assert.Equal(3, imageEntries.Length);
        Assert.Single(imageEntries, entry => entry.State == EntityState.Deleted);
        Assert.All(imageEntries, entry =>
            Assert.Equal(productId, entry.Property<Guid>("ProductId").CurrentValue));
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static CatalogMediaReference Media(string name) =>
        CatalogMediaReference.Create($"brands/{name}.webp", $"/media/brands/{name}.webp", $"โลโก้ {name}");

    private static ProductImageDefinition Image(Guid id, string storageKey, string altText) => new(
        id,
        storageKey,
        $"/media/{storageKey}",
        altText);

    private static async Task<SeededImageProduct> SeedImageProductAsync(
        ApplicationDbContext db,
        string key)
    {
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brandDisplayName = $"Image Brand {key}";
        var brand = Brand.Create(
            Guid.NewGuid(), brandDisplayName, $"Image Brand {key}", CatalogSlug.Create($"image-brand-{key}"), now, "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), $"Image Product {key}", $"Image Product {key}", "Description", $"image-product-{key}",
            CatalogSeedIds.GundamCategory, brand.Id, CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)), now, "test");
        product.AddImage(Guid.NewGuid(), $"images/{key}/front", $"/media/{key}/front", "front", now, "test");
        product.AddImage(Guid.NewGuid(), $"images/{key}/side", $"/media/{key}/side", "side", now, "test");
        product.AddImage(Guid.NewGuid(), $"images/{key}/back", $"/media/{key}/back", "back", now, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededImageProduct(product.Id, brandDisplayName, now);
    }

    private sealed record SeededImageProduct(
        Guid ProductId,
        string BrandDisplayName,
        DateTimeOffset Now);

    private sealed class RollbackFailureInterceptor : DbTransactionInterceptor
    {
        internal CancellationToken RollbackToken { get; private set; }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            RollbackToken = cancellationToken;
            throw new InjectedRollbackException();
        }
    }

    private sealed class InjectedRollbackException : Exception;
}
