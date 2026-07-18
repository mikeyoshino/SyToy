namespace ToyStore.Domain.Catalog;

public sealed record UniverseSeedDefinition(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string NormalizedDisplayName,
    string NormalizedEnglishName,
    string Slug,
    CatalogReferenceStatus Status,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy);

public static class UniverseSeeds
{
    public static IReadOnlyList<UniverseSeedDefinition> All => Validate(
    [
        new(
            CatalogSeedIds.MarvelUniverse,
            "Marvel",
            "Marvel",
            "MARVEL",
            "MARVEL",
            "marvel",
            CatalogReferenceStatus.Active,
            CatalogSeedIds.AuditInstantUtc,
            CatalogSeedIds.AuditActor),
        new(
            CatalogSeedIds.DcUniverse,
            "DC",
            "DC",
            "DC",
            "DC",
            "dc",
            CatalogReferenceStatus.Active,
            CatalogSeedIds.AuditInstantUtc,
            CatalogSeedIds.AuditActor),
        new(
            CatalogSeedIds.UnknownUniverse,
            "Unknown",
            "Unknown",
            "UNKNOWN",
            "UNKNOWN",
            "unknown",
            CatalogReferenceStatus.Active,
            CatalogSeedIds.AuditInstantUtc,
            CatalogSeedIds.AuditActor),
    ]);

    internal static IReadOnlyList<UniverseSeedDefinition> Validate(
        IEnumerable<UniverseSeedDefinition> definitions)
    {
        var snapshot = definitions.ToArray();
        if (snapshot.Select(definition => definition.Id).Distinct().Count() != snapshot.Length)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SeedIdentityDuplicate);
        }

        if (snapshot.Select(definition => definition.Slug)
            .Distinct(StringComparer.Ordinal)
            .Count() != snapshot.Length)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SeedSlugDuplicate);
        }

        return Array.AsReadOnly(snapshot);
    }
}
