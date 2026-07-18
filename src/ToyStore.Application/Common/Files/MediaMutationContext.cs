using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Common.Files;

public sealed record MediaMutationContext
{
    public MediaMutationContext(
        string entityType,
        Guid entityId,
        CatalogMediaReference? previousMedia)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("A media mutation requires an entity identity.", nameof(entityId));
        }

        EntityType = entityType;
        EntityId = entityId;
        PreviousMedia = previousMedia;
    }

    public string EntityType { get; init; }

    public Guid EntityId { get; init; }

    public CatalogMediaReference? PreviousMedia { get; init; }
}
