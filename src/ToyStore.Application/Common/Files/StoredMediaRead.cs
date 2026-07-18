namespace ToyStore.Application.Common.Files;

public sealed class StoredMediaRead : IDisposable, IAsyncDisposable
{
    public StoredMediaRead(
        Stream content,
        string contentType,
        long length,
        DateTimeOffset lastModifiedUtc,
        string entityTag)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityTag);

        Content = content;
        ContentType = contentType;
        Length = length;
        LastModifiedUtc = lastModifiedUtc;
        EntityTag = entityTag;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    public long Length { get; }

    public DateTimeOffset LastModifiedUtc { get; }

    public string EntityTag { get; }

    public void Dispose() => Content.Dispose();

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
