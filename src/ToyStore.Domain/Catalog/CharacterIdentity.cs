namespace ToyStore.Domain.Catalog;

public sealed record CharacterIdentity
{
    private CharacterIdentity(Guid universeId, string normalizedName)
    {
        UniverseId = universeId;
        NormalizedName = normalizedName;
    }

    public Guid UniverseId { get; }

    public string NormalizedName { get; }

    public static CharacterIdentity Create(Guid universeId, string name)
    {
        if (universeId == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.UniverseRequired);
        }

        return new CharacterIdentity(universeId, CatalogNameNormalizer.Normalize(name));
    }

    internal static CharacterIdentity FromNormalized(Guid universeId, string normalizedName)
    {
        if (universeId == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.UniverseRequired);
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.NameRequired);
        }

        return new CharacterIdentity(universeId, normalizedName);
    }
}
