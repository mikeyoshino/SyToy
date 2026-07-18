namespace ToyStore.Domain.Catalog;

public static class CatalogReferenceLimits
{
    public const int NameLength = 200;
    public const int SlugLength = 240;
    public const int ActorLength = 200;
    public const int StorageKeyLength = 500;
    public const int PublicRelativeUrlLength = 1000;
    public const int AltTextLength = 500;

    internal static PreparedCatalogName PrepareName(string value)
    {
        var normalized = CatalogNameNormalizer.Normalize(value);
        var trimmed = value.Trim();

        if (trimmed.Length > NameLength)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.NameTooLong);
        }

        if (normalized.Length > NameLength)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.NormalizedNameTooLong);
        }

        return new PreparedCatalogName(trimmed, normalized);
    }

    internal readonly record struct PreparedCatalogName(string Persisted, string Normalized);
}
