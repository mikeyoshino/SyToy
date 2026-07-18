using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes;

public interface IUniverseMutationSessionFactory
{
    ValueTask<IUniverseMutationSession> OpenAsync(CancellationToken cancellationToken);

    Task<CatalogCommitVerification<UniverseMutationEvidence>> VerifyCommitAsync(
        UniverseMutationEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IUniverseMutationSession : ICatalogMutationSession
{
    Task AcquireMutationLockAsync(CancellationToken cancellationToken);

    Task<Universe?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DisplayNameExistsAsync(
        string normalizedDisplayName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task<bool> EnglishNameExistsAsync(
        string normalizedEnglishName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task<CatalogSlug> AllocateSlugAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    void Add(Universe universe);
}

public sealed record UniverseMutationEvidence
{
    private UniverseMutationEvidence(Universe universe)
    {
        Id = universe.Id;
        IntendedVersion = universe.Version;
        DisplayName = universe.DisplayName;
        EnglishName = universe.EnglishName;
        Slug = universe.Slug.Value;
        LogoStorageKey = universe.Logo?.StorageKey;
        Status = universe.Status;
    }

    public Guid Id { get; }

    public long IntendedVersion { get; }

    public string DisplayName { get; }

    public string EnglishName { get; }

    public string Slug { get; }

    public string? LogoStorageKey { get; }

    public CatalogReferenceStatus Status { get; }

    public static UniverseMutationEvidence Capture(Universe universe)
    {
        ArgumentNullException.ThrowIfNull(universe);
        return new UniverseMutationEvidence(universe);
    }
}
