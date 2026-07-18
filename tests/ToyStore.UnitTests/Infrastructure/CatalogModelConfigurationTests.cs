using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class CatalogModelConfigurationTests
{
    private static readonly IModel Model = CreateModel();

    [Fact]
    public void ModelKeepsIdentityAndMapsExactlyTheApprovedCatalogEntities()
    {
        Assert.Equal("AspNetUsers", Entity<ApplicationUser>().GetTableName());
        Assert.Equal("AspNetRoles", Entity<IdentityRole>().GetTableName());
        Assert.Equal("Products", Entity<Product>().GetTableName());
        Assert.Equal("ProductImages", Entity<ProductImage>().GetTableName());
        Assert.Equal("ProductCharacters", Entity<ProductCharacter>().GetTableName());
        Assert.Equal("ProductCategories", Entity<ProductCategory>().GetTableName());
        Assert.Equal("Brands", Entity<Brand>().GetTableName());
        Assert.Equal("Universes", Entity<Universe>().GetTableName());
        Assert.Equal("Characters", Entity<Character>().GetTableName());
        Assert.Contains(Model.GetEntityTypes(), type =>
            type.GetTableName() == "MediaCleanupEntries");
        Assert.DoesNotContain(Model.GetEntityTypes(), type =>
            type.ClrType.Name.Contains("Variant", StringComparison.OrdinalIgnoreCase)
            || type.ClrType.Name.Contains("Sku", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductModelHasOfferChecksUtcColumnsRestrictReferencesAndOwnedFields()
    {
        var product = Entity<Product>();
        Assert.Equal("numeric", product.FindProperty("InStockOffer.Price")?.GetColumnType()
            ?? Model.GetEntityTypes().Single(type => type.ClrType == typeof(InStockOffer)).FindProperty(nameof(InStockOffer.Price))!.GetColumnType());
        Assert.Equal("timestamp with time zone", product.FindProperty(nameof(Product.CreatedAtUtc))!.GetColumnType());
        Assert.Equal(3, product.GetForeignKeys().Count(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict));
        var checks = product.GetCheckConstraints().Select(value => value.Name).ToHashSet();
        Assert.Contains("CK_Products_Offer_Matches_SaleType", checks);
        Assert.Contains("CK_Products_PreOrder_Amounts", checks);
        Assert.Contains("CK_Products_PreOrder_Capacity", checks);
        Assert.Contains("CK_Products_PreOrder_CloseAfterCreated", checks);
        Assert.Contains("CK_Products_PreOrder_BangkokCloseTime", checks);
        Assert.Contains("CK_Products_PreOrder_EtaNotBeforeCloseMonth", checks);
        Assert.Contains("CK_Products_Slug_Format", checks);
        Assert.Contains(product.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Product.Slug));
    }

    [Fact]
    public void ImageAndCharacterRelationsHaveRequiredUniquenessAndDeleteBehavior()
    {
        var image = Entity<ProductImage>();
        Assert.Contains(image.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"IsPrimary\"");
        Assert.Contains(image.GetIndexes(), index => index.IsUnique && index.Properties.Select(value => value.Name).SequenceEqual(["ProductId", nameof(ProductImage.SortOrder)]));
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(image.GetForeignKeys()).DeleteBehavior);

        var link = Entity<ProductCharacter>();
        Assert.Equal([nameof(ProductCharacter.ProductId), nameof(ProductCharacter.CharacterId)], link.FindPrimaryKey()!.Properties.Select(value => value.Name));
        Assert.Contains(link.GetForeignKeys(), value => value.PrincipalEntityType.ClrType == typeof(Character) && value.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(link.GetForeignKeys(), value => value.PrincipalEntityType.ClrType == typeof(Product) && value.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public void ReferenceEntitiesHaveNormalizedAndScopedUniqueIndexesAndLiteralSeeds()
    {
        AssertUnique<Brand>(nameof(Brand.NormalizedDisplayName));
        AssertUnique<Brand>(nameof(Brand.NormalizedEnglishName));
        AssertUnique<Brand>(nameof(Brand.Slug));
        AssertUnique<Universe>(nameof(Universe.NormalizedDisplayName));
        AssertUnique<Universe>(nameof(Universe.NormalizedEnglishName));
        AssertUnique<Universe>(nameof(Universe.Slug));
        var brandMediaCheck = Assert.Single(
            Entity<Brand>().GetCheckConstraints(),
            constraint => constraint.Name == "CK_Brands_Image_AllNullOrPresent");
        var universeMediaCheck = Assert.Single(
            Entity<Universe>().GetCheckConstraints(),
            constraint => constraint.Name == "CK_Universes_Logo_AllNullOrPresent");
        Assert.Contains("[^[:space:]]", brandMediaCheck.Sql, StringComparison.Ordinal);
        Assert.Contains("[^[:space:]]", universeMediaCheck.Sql, StringComparison.Ordinal);
        Assert.Contains(Entity<Character>().GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(value => value.Name).SequenceEqual([nameof(Character.UniverseId), nameof(Character.NormalizedName)]));

        var categories = Entity<ProductCategory>().GetSeedData();
        Assert.Equal(2, categories.Count());
        var universes = Entity<Universe>().GetSeedData();
        Assert.Equal(3, universes.Count());
        Assert.All(universes, row =>
        {
            Assert.Equal(CatalogSeedIds.AuditInstantUtc, row[nameof(Universe.CreatedAtUtc)]);
            Assert.Equal(CatalogSeedIds.AuditActor, row[nameof(Universe.CreatedBy)]);
            Assert.Equal(1L, row[nameof(Universe.Version)]);
        });
    }

    [Fact]
    public void BrandAndUniverseVersionsAreRequiredBigintConcurrencyTokens()
    {
        AssertVersion<Brand>();
        AssertVersion<Universe>();
        AssertVersion<Product>();
        Assert.Contains(
            Entity<Product>().GetCheckConstraints(),
            constraint => constraint.Name == "CK_Products_Version_Positive");
    }

    [Fact]
    public void CleanupLedgerHasDurableFieldsAndOneUnresolvedRowPerStorageKey()
    {
        var ledger = Model.GetEntityTypes().Single(type =>
            type.GetTableName() == "MediaCleanupEntries");
        Assert.Equal(typeof(Guid), ledger.FindProperty("Id")!.ClrType);
        Assert.Equal(typeof(string), ledger.FindProperty("StorageKey")!.ClrType);
        Assert.Equal(typeof(ToyStore.Application.Common.Files.MediaCleanupReason), ledger.FindProperty("Reason")!.ClrType);
        Assert.Equal("character varying(64)", ledger.FindProperty("Reason")!.GetColumnType());
        Assert.Equal(typeof(string), ledger.FindProperty("EntityType")!.ClrType);
        Assert.Equal(typeof(Guid), ledger.FindProperty("EntityId")!.ClrType);
        Assert.Equal("timestamp with time zone", ledger.FindProperty("FirstObservedAtUtc")!.GetColumnType());
        Assert.Equal("timestamp with time zone", ledger.FindProperty("LastAttemptAtUtc")!.GetColumnType());
        Assert.Equal(typeof(int), ledger.FindProperty("AttemptCount")!.ClrType);
        Assert.Equal("timestamp with time zone", ledger.FindProperty("ResolvedAtUtc")!.GetColumnType());

        var unresolved = Assert.Single(ledger.GetIndexes(), index => index.IsUnique);
        Assert.Equal(["StorageKey"], unresolved.Properties.Select(property => property.Name));
        Assert.Equal("\"ResolvedAtUtc\" IS NULL", unresolved.GetFilter());
        Assert.Contains(
            ledger.GetCheckConstraints(),
            constraint => constraint.Name == "CK_MediaCleanupEntries_AttemptCount_Positive");
    }

    [Fact]
    public void CurrentModelMatchesTheLatestCodeFirstSnapshot()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_model_test;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static IEntityType Entity<TEntity>() =>
        Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"Missing EF entity {typeof(TEntity).Name}.");

    private static void AssertUnique<TEntity>(string propertyName) =>
        Assert.Contains(Entity<TEntity>().GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == propertyName);

    private static void AssertVersion<TEntity>()
    {
        var version = Entity<TEntity>().FindProperty("Version")!;
        Assert.Equal(typeof(long), version.ClrType);
        Assert.Equal("bigint", version.GetColumnType());
        Assert.False(version.IsNullable);
        Assert.True(version.IsConcurrencyToken);
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=toystore_model_test;Username=test;Password=test",
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        using var context = new ApplicationDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
