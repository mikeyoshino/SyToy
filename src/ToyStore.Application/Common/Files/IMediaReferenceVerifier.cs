using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Common.Files;

public interface IMediaReferenceVerifier
{
    Task<MediaReferenceVerification> VerifyAsync(
        TrustedMediaStorageKey storageKey,
        CancellationToken cancellationToken);
}

public enum MediaReferenceVerification
{
    Referenced = 1,
    Unreferenced = 2,
    Unavailable = 3,
}

public sealed record TrustedMediaStorageKey
{
    private TrustedMediaStorageKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public static TrustedMediaStorageKey From(StagedMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new TrustedMediaStorageKey(media.StorageKey);
    }

    public static TrustedMediaStorageKey From(CatalogMediaReference media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new TrustedMediaStorageKey(media.StorageKey);
    }

    internal static TrustedMediaStorageKey From(ProductImage media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new TrustedMediaStorageKey(media.StorageKey);
    }

    internal static TrustedMediaStorageKey FromThumbnail(StagedMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new TrustedMediaStorageKey(media.ThumbnailStorageKey
            ?? throw new ArgumentException("Staged media has no thumbnail key.", nameof(media)));
    }

    internal static TrustedMediaStorageKey FromThumbnail(ProductImage media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new TrustedMediaStorageKey(media.ThumbnailStorageKey
            ?? throw new ArgumentException("Product image has no thumbnail key.", nameof(media)));
    }
}
