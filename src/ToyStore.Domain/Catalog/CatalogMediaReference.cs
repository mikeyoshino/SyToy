namespace ToyStore.Domain.Catalog;

public sealed record CatalogMediaReference
{
    private CatalogMediaReference()
    {
        StorageKey = null!;
        PublicRelativeUrl = null!;
        AltText = null!;
    }

    private CatalogMediaReference(string storageKey, string publicRelativeUrl, string altText)
    {
        StorageKey = storageKey;
        PublicRelativeUrl = publicRelativeUrl;
        AltText = altText;
    }

    public string StorageKey { get; private set; }

    public string PublicRelativeUrl { get; private set; }

    public string AltText { get; private set; }

    public static CatalogMediaReference Create(
        string storageKey,
        string publicRelativeUrl,
        string altText)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.MediaStorageKeyRequired);
        }

        if (string.IsNullOrWhiteSpace(publicRelativeUrl))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.MediaRelativeUrlRequired);
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.MediaAltTextRequired);
        }

        return new CatalogMediaReference(storageKey, publicRelativeUrl, altText);
    }
}
