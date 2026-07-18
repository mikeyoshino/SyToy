using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands;

public interface IBrandMutationSessionFactory
{
    ValueTask<IBrandMutationSession> OpenAsync(CancellationToken cancellationToken);

    Task<CatalogCommitVerification<BrandMutationEvidence>> VerifyCommitAsync(
        BrandMutationEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IBrandMutationSession : ICatalogMutationSession
{
    Task AcquireMutationLockAsync(CancellationToken cancellationToken);

    Task<Brand?> FindAsync(Guid id, CancellationToken cancellationToken);

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

    void Add(Brand brand);
}

public sealed record BrandMutationEvidence
{
    private BrandMutationEvidence(Brand brand)
    {
        Id = brand.Id;
        IntendedVersion = brand.Version;
        DisplayName = brand.DisplayName;
        EnglishName = brand.EnglishName;
        Slug = brand.Slug.Value;
        ImageStorageKey = brand.Image?.StorageKey;
        Status = brand.Status;
    }

    public Guid Id { get; }

    public long IntendedVersion { get; }

    public string DisplayName { get; }

    public string EnglishName { get; }

    public string Slug { get; }

    public string? ImageStorageKey { get; }

    public CatalogReferenceStatus Status { get; }

    public static BrandMutationEvidence Capture(Brand brand)
    {
        ArgumentNullException.ThrowIfNull(brand);
        return new BrandMutationEvidence(brand);
    }
}
