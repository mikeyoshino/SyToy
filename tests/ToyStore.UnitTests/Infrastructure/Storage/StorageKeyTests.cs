using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class StorageKeyTests
{
    private const string Batch = "aabbccddeeff00112233445566778899";
    private const string FileId = "00112233445566778899aabbccddeeff";

    [Theory]
    [InlineData("../a.jpg")]
    [InlineData("/absolute/a.jpg")]
    [InlineData("aabbccddeeff00112233445566778899/../a.jpg")]
    [InlineData("aabbccddeeff00112233445566778899\\a.jpg")]
    [InlineData("aabbccddeeff00112233445566778899/a.svg")]
    [InlineData("AABBCCDDEEFF00112233445566778899/00112233445566778899aabbccddeeff.webp")]
    [InlineData("aabbccddeeff00112233445566778899/00112233445566778899aabbccddeeff.webp\n")]
    public void RejectsForgedOrNonCanonicalKeys(string value)
    {
        Assert.False(StorageKey.TryParse(value, out _));
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("webp")]
    public void AcceptsOnlyFixedTwoSegmentGeneratedGrammar(string extension)
    {
        var value = $"{Batch}/{FileId}.{extension}";

        Assert.True(StorageKey.TryParse(value, out var parsed));
        Assert.Equal(Batch, parsed.BatchId);
        Assert.Equal($"{FileId}.{extension}", parsed.FileName);
        Assert.True(value.Length < 500);
        Assert.True($"/media/{value}".Length < 1000);
    }

    [Fact]
    public void GeneratorUsesSixteenCryptographicRandomBytesAndProducesUniqueGrammarValues()
    {
        var generator = new MediaIdGenerator();
        var ids = Enumerable.Range(0, 1000).Select(_ => generator.CreateId()).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.True(StorageKey.IsBatchId(id)));
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ToyStore.Infrastructure",
            "Storage",
            "MediaIdGenerator.cs"));
        Assert.Contains("RandomNumberGenerator", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid", source, StringComparison.Ordinal);
    }

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
