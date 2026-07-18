namespace ToyStore.UnitTests.Architecture;

public sealed class MediaStorageDocumentationTests
{
    [Fact]
    public void ArchitectureDocumentsStagingCommitPublicReadAndNoCrossResourceAtomicity()
    {
        var architecture = Read("docs", "ARCHITECTURE.md");

        Assert.Contains(".staging/{batch-id}", architecture, StringComparison.Ordinal);
        Assert.Contains("files/{batch-id}", architecture, StringComparison.Ordinal);
        Assert.Contains("5 MiB", architecture, StringComparison.Ordinal);
        Assert.Contains("CSPRNG 16 bytes", architecture, StringComparison.Ordinal);
        Assert.Contains(".owner", architecture, StringComparison.Ordinal);
        Assert.Contains("rename แบบ non-overwriting", architecture, StringComparison.Ordinal);
        Assert.Contains("/media/{batch-id}/{file-name}", architecture, StringComparison.Ordinal);
        Assert.Contains("ไม่เป็น atomic resource เดียวกัน", architecture, StringComparison.Ordinal);
        Assert.Contains("age-graced reconciliation", architecture, StringComparison.Ordinal);
        Assert.Contains("ไม่มี cleanup worker", architecture, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentQuiescesOneRestoreSetAndSystemdRestrictsWrites()
    {
        var deployment = Read("docs", "DEPLOYMENT.md");
        var service = Read("deploy", "toystore.service.example");

        Assert.Contains("sudo systemctl stop toystore", deployment, StringComparison.Ordinal);
        Assert.Contains("uploads/files", deployment, StringComparison.Ordinal);
        Assert.Contains("restore set เดียวกัน", deployment, StringComparison.Ordinal);
        Assert.Contains("นอก server", deployment, StringComparison.Ordinal);
        Assert.Contains("backup_id=\"$(date -u +%Y%m%dT%H%M%S%NZ)\"", deployment, StringComparison.Ordinal);
        Assert.Contains("sudo -u toystore sh -c 'set -C; cat > \"$1\"'", deployment, StringComparison.Ordinal);
        Assert.Equal(
            2,
            deployment.Split(
                "sudo -u toystore sh -c 'set -C; cat > \"$1\"'",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("tar -C /var/lib/toystore -czf - uploads/files keys", deployment, StringComparison.Ordinal);
        Assert.Contains("--keep-old-files", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("toystore-20260718", deployment, StringComparison.Ordinal);
        Assert.Contains("UMask=0027", service, StringComparison.Ordinal);
        Assert.Contains("ReadWritePaths=/var/lib/toystore/uploads", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Storage__BackupPath", deployment, StringComparison.Ordinal);
    }

    [Fact]
    public void FutureProductSliceOwnsExplicitCompensationContract()
    {
        var tasks = Read("TASKS.md");
        var m5Start = tasks.IndexOf("**M5-03**", StringComparison.Ordinal);
        var m5End = tasks.IndexOf("**M5-04**", m5Start, StringComparison.Ordinal);
        var m5 = tasks[m5Start..m5End];

        Assert.Contains("non-cancelled cleanup", m5, StringComparison.Ordinal);
        Assert.Contains("delete replaced media only after durable database commit", m5, StringComparison.Ordinal);
        Assert.Contains("returns the already-committed success", m5, StringComparison.Ordinal);
        Assert.Contains("never signal false rollback/retry", m5, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
