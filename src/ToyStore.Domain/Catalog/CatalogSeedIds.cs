namespace ToyStore.Domain.Catalog;

public static class CatalogSeedIds
{
    public static readonly Guid ArtToyCategory =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly Guid GundamCategory =
        Guid.Parse("10000000-0000-0000-0000-000000000002");

    public static readonly Guid MarvelUniverse =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static readonly Guid DcUniverse =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static readonly Guid UnknownUniverse =
        Guid.Parse("20000000-0000-0000-0000-000000000003");

    public static readonly DateTimeOffset AuditInstantUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public const string AuditActor = "system:catalog-seed";
}
