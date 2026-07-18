namespace ToyStore.Application.Common.Files;

public sealed record MediaUpload
{
    public MediaUpload(Stream content, string contentType, bool generateProductThumbnail = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);

        Content = content;
        ContentType = contentType;
        GenerateProductThumbnail = generateProductThumbnail;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    public bool GenerateProductThumbnail { get; }
}
