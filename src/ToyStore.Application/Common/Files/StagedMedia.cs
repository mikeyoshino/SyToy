namespace ToyStore.Application.Common.Files;

public sealed record StagedMedia(
    string BatchToken,
    string StorageKey,
    string PublicRelativeUrl,
    string ContentType,
    long Length);
