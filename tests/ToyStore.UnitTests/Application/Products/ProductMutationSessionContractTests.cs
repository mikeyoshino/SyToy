using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;

namespace ToyStore.UnitTests.Application.Products;

public sealed class ProductMutationSessionContractTests
{
    [Fact]
    public void SessionIsNarrowProviderNeutralAndOnceOnly()
    {
        var methods = typeof(IProductMutationSession).GetMethods()
            .Concat(typeof(ICatalogMutationSession).GetMethods())
            .ToArray();

        Assert.Contains(methods, method => method.Name == nameof(IProductMutationSession.ExecuteOnceAsync));
        Assert.Contains(methods, method => method.Name == nameof(IProductMutationSession.AcquireNamespaceLockAsync));
        Assert.Contains(methods, method => method.Name == nameof(IProductMutationSession.LockProductAsync));
        Assert.Contains(methods, method => method.Name == nameof(IProductMutationSession.LockReferencesAsync));
        Assert.Contains(methods, method => method.Name == nameof(IProductMutationSession.Add));
        var signatures = string.Join('\n', methods.Select(method => method.ToString()));
        Assert.DoesNotContain("EntityFrameworkCore", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("DbSet", signatures, StringComparison.Ordinal);
        Assert.DoesNotContain("IQueryable", signatures, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceCapturesExactProductAndCreateInventory()
    {
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var imageId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var product = Product.CreateInStock(
            Guid.NewGuid(), "สินค้า", "Product", "รายละเอียด", "product",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InStockOffer.Create(Money.Create(100)),
            [new ProductImageDefinition(imageId, "batch/image.webp", "/media/batch/image.webp", "สินค้า")],
            [characterId], now, "admin");
        var inventory = InventoryItem.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), 0,
            "สินค้าเริ่มต้น", "product-create", now, "admin");

        var evidence = ProductMutationEvidence.Capture(product, inventory);

        Assert.Equal(1, evidence.IntendedVersion);
        Assert.Equal(imageId, Assert.Single(evidence.Images).Id);
        Assert.True(evidence.Images[0].IsPrimary);
        Assert.Equal(characterId, Assert.Single(evidence.CharacterIds));
        Assert.True(evidence.HasInventoryCreation);
        Assert.Equal(0, evidence.InventoryEvidence.IntendedOnHandQuantity);
        Assert.Equal(StockMovementType.InitialStock, evidence.InventoryEvidence.MovementType);
        Assert.Equal(now, evidence.InventoryCreatedAtUtc);
        Assert.Equal("admin", evidence.InventoryCreatedBy);
    }

    [Fact]
    public void InfrastructureRegistersSingletonProductSessionFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_unit_test;Username=test;Password=test",
            })
            .Build());

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IProductMutationSessionFactory));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void VerificationUsesExplicitCatalogOutcomes()
    {
        Assert.Equal(
            [
                CatalogCommitVerification.Committed,
                CatalogCommitVerification.Superseded,
                CatalogCommitVerification.NotCommitted,
                CatalogCommitVerification.Unavailable,
                CatalogCommitVerification.Inconsistent,
            ],
            Enum.GetValues<CatalogCommitVerification>());
    }
}
