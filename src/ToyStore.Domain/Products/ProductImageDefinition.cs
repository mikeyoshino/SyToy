namespace ToyStore.Domain.Products;

public sealed record ProductImageDefinition(
    Guid Id,
    string StorageKey,
    string PublicRelativeUrl,
    string AltText);
