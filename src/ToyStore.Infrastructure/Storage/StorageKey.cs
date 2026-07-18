using System.Text.RegularExpressions;

namespace ToyStore.Infrastructure.Storage;

internal sealed partial record StorageKey(string BatchId, string FileName, string Extension)
{
    internal string Value => $"{BatchId}/{FileName}";

    internal static bool TryParse(string? value, out StorageKey parsed)
    {
        parsed = null!;
        if (value is null)
        {
            return false;
        }

        var match = KeyPattern().Match(value);
        if (!match.Success)
        {
            return false;
        }

        parsed = new StorageKey(
            match.Groups["batch"].Value,
            match.Groups["file"].Value,
            match.Groups["extension"].Value);
        return true;
    }

    internal static bool IsBatchId(string? value) =>
        value is not null && BatchPattern().IsMatch(value);

    [GeneratedRegex(
        "\\A(?<batch>[a-f0-9]{32})/(?<file>[a-f0-9]{32}\\.(?<extension>jpg|png|webp))\\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("\\A[a-f0-9]{32}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex BatchPattern();
}
