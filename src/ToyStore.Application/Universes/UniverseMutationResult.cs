using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes;

public sealed record UniverseMutationResult(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Slug,
    long Version,
    CatalogReferenceStatus Status)
{
    public static UniverseMutationResult From(Universe universe)
    {
        ArgumentNullException.ThrowIfNull(universe);
        return new UniverseMutationResult(
            universe.Id,
            universe.DisplayName,
            universe.EnglishName,
            universe.Slug.Value,
            universe.Version,
            universe.Status);
    }

    public static UniverseMutationResult From(UniverseMutationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new UniverseMutationResult(
            evidence.Id,
            evidence.DisplayName,
            evidence.EnglishName,
            evidence.Slug,
            evidence.IntendedVersion,
            evidence.Status);
    }
}
