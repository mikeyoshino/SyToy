namespace ToyStore.Domain.Catalog;

public sealed class Character
{
    private Guid _id;
    private Guid _universeId;
    private string _name = null!;
    private string _normalizedName = null!;

    private Character()
    {
    }

    private Character(
        Guid id,
        Guid universeId,
        CatalogReferenceLimits.PreparedCatalogName preparedName)
    {
        _id = id;
        _universeId = universeId;
        _name = preparedName.Persisted;
        _normalizedName = preparedName.Normalized;
    }

    public Guid Id => _id;

    public Guid UniverseId => _universeId;

    public string Name => _name;

    public string NormalizedName => _normalizedName;

    public CharacterIdentity Identity => CharacterIdentity.FromNormalized(UniverseId, NormalizedName);

    public static Character Create(Guid id, Guid universeId, string name)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.IdentityRequired);
        }

        if (universeId == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.UniverseRequired);
        }

        var preparedName = CatalogReferenceLimits.PrepareName(name);
        return new Character(id, universeId, preparedName);
    }
}
