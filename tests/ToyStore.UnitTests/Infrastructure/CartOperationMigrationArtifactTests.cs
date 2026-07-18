namespace ToyStore.UnitTests.Infrastructure;

public sealed class CartOperationMigrationArtifactTests
{
    [Fact]
    public void MigrationAddsSafeIdempotencyEvidenceWithoutBrowserPayload()
    {
        var migrations = Path.Combine(Root(), "src", "ToyStore.Infrastructure", "Persistence", "Migrations");
        var migration = Directory.GetFiles(migrations, "*AddCartOperationIdempotency.cs").Single();
        var source = File.ReadAllText(migration);
        Assert.Contains("CartOperations", source, StringComparison.Ordinal);
        Assert.Contains("IntentFingerprint", source, StringComparison.Ordinal);
        Assert.Contains("ResultingCartVersion", source, StringComparison.Ordinal);
        Assert.Contains("FK_CartOperations_Carts_CartId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Payload", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Price", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stock", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdditiveMigrationPersistsExactSafeRetryResult()
    {
        var migrations = Path.Combine(Root(), "src", "ToyStore.Infrastructure", "Persistence", "Migrations");
        var migration = Directory.GetFiles(migrations, "*AddCartOperationResultReplay.cs").Single();
        var source = File.ReadAllText(migration);

        Assert.Contains("ResultingTotalQuantity", source, StringComparison.Ordinal);
        Assert.Contains("ResultData", source, StringComparison.Ordinal);
        Assert.Contains("jsonb", source, StringComparison.Ordinal);
        Assert.Contains("CK_CartOperations_ResultData_ByType", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Price", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stock", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
