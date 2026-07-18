namespace ToyStore.Application.Common.Files;

public sealed record MediaUpload
{
    public MediaUpload(Stream content, string contentType)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(contentType);

        Content = content;
        ContentType = contentType;
    }

    public Stream Content { get; }

    public string ContentType { get; }
}
