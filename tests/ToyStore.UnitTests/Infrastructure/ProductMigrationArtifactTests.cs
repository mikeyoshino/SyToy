namespace ToyStore.UnitTests.Infrastructure;

public sealed class ProductMigrationArtifactTests
{
    [Fact]
    public void IdempotentDeltaContainsOnlyProductVersionAndAuditChecks()
    {
        var sql = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "artifacts",
            "migrations",
            "AddProductVersion.sql"));

        Assert.Contains(
            "ALTER TABLE \"Products\" ADD \"Version\" bigint NOT NULL DEFAULT 1",
            sql,
            StringComparison.Ordinal);
        Assert.Contains("CK_Products_Version_Positive", sql, StringComparison.Ordinal);
        Assert.Contains("CK_Products_Audit_Chronology", sql, StringComparison.Ordinal);
        Assert.Contains("CK_Products_Lifecycle_Audit", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE \"AspNet", sql, StringComparison.Ordinal);
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
