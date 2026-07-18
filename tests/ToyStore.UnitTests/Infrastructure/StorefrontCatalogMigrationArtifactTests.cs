namespace ToyStore.UnitTests.Infrastructure;

public sealed class StorefrontCatalogMigrationArtifactTests
{
    [Fact]
    public void MigrationAddsSupportingInStockPriceIndex()
    {
        var root = Root();
        var migrations = Path.Combine(root, "src", "ToyStore.Infrastructure", "Persistence", "Migrations");
        var migration = Directory.GetFiles(migrations, "*AddStorefrontCatalogPriceIndex.cs").Single();
        var source = File.ReadAllText(migration);
        Assert.Contains("IX_Products_InStockPrice", source, StringComparison.Ordinal);
        Assert.Contains("CreateIndex", source, StringComparison.Ordinal);
        Assert.Contains("DropIndex", source, StringComparison.Ordinal);
    }
    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
