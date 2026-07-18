using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Architecture;

public sealed class InventoryDomainArchitectureTests
{
    [Fact]
    public void InventoryRemainsDomainOnlyAndProductOwnsNoStockCounters()
    {
        var root = RepositoryRoot();
        var inventorySources = string.Join(
            '\n',
            Directory.EnumerateFiles(
                    Path.Combine(root, "src", "ToyStore.Domain", "Inventory"),
                    "*.cs")
                .Select(File.ReadAllText));
        foreach (var forbidden in new[]
                 {
                     "EntityFrameworkCore",
                     "Npgsql",
                     "IQueryable",
                     "DbSet",
                     "TimeProvider",
                     "DateTimeOffset.UtcNow",
                     "Repository",
                     "Warehouse",
                     "Sku",
                     "Lot",
                 })
        {
            Assert.DoesNotContain(forbidden, inventorySources, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            typeof(Product).GetProperties(),
            property => property.Name is "InventoryItem" or "OnHandQuantity"
                or "HeldQuantity" or "AvailableQuantity" or "ReservableQuantity");
    }

    [Fact]
    public void OnHandMutationsExposeMandatoryEvidenceAndNoBatchTransactionClaim()
    {
        Assert.Equal(
            typeof(InventoryCreation),
            typeof(InventoryItem).GetMethod(nameof(InventoryItem.Create))!.ReturnType);
        Assert.Equal(
            typeof(StockMovement),
            typeof(InventoryItem).GetMethod(nameof(InventoryItem.ReceiveStock))!.ReturnType);
        Assert.Equal(
            typeof(StockMovement),
            typeof(InventoryItem).GetMethod(nameof(InventoryItem.AdjustStock))!.ReturnType);
        Assert.Equal(
            typeof(ReservationTransitionResult),
            typeof(InventoryItem).GetMethod(nameof(InventoryItem.ConsumeReservation))!.ReturnType);

        Assert.DoesNotContain(
            typeof(InventoryItem).Assembly.GetTypes(),
            type => type.Namespace == typeof(InventoryItem).Namespace
                && (type.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase)
                    || type.Name.Contains("Transaction", StringComparison.OrdinalIgnoreCase)));
    }

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
