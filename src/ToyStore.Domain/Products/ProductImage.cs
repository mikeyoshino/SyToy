namespace ToyStore.Domain.Products;

public sealed class ProductImage
{
    private ProductImage()
    {
        StorageKey = null!;
        PublicRelativeUrl = null!;
        AltText = null!;
    }

    internal ProductImage(
        Guid id,
        string storageKey,
        string publicRelativeUrl,
        string altText,
        int sortOrder)
    {
        Id = id;
        StorageKey = storageKey;
        PublicRelativeUrl = publicRelativeUrl;
        AltText = altText;
        SortOrder = sortOrder;
        IsPrimary = sortOrder == 0;
    }

    public Guid Id { get; private set; }

    public string StorageKey { get; private set; }

    public string PublicRelativeUrl { get; private set; }

    public string AltText { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    internal void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
        IsPrimary = sortOrder == 0;
    }
}
