namespace ToyStore.UnitTests.Infrastructure;

public sealed class CartMigrationArtifactTests
{
    [Fact]
    public void MigrationCreatesCustomerOwnedPriceAndStockFreeCartSchema()
    {
        var migrations = Path.Combine(
            Root(), "src", "ToyStore.Infrastructure", "Persistence", "Migrations");
        var migration = Directory.GetFiles(migrations, "*AddCustomerCartFoundation.cs").Single();
        var source = File.ReadAllText(migration);

        Assert.Contains("CreateTable(\n                name: \"Carts\"", source, StringComparison.Ordinal);
        Assert.Contains("CreateTable(\n                name: \"CartItems\"", source, StringComparison.Ordinal);
        Assert.Contains("UX_Carts_CustomerId", source, StringComparison.Ordinal);
        Assert.Contains("CK_CartItems_Quantity_Bounds", source, StringComparison.Ordinal);
        Assert.Contains("FK_Carts_AspNetUsers_CustomerId", source, StringComparison.Ordinal);
        Assert.Contains("FK_CartItems_Products_ProductId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Price", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stock", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AnonymousCart", source, StringComparison.Ordinal);
    }

    private static string Root()
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

        throw new DirectoryNotFoundException();
    }
}
