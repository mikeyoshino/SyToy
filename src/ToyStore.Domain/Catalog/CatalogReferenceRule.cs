namespace ToyStore.Domain.Catalog;

public enum CatalogReferenceRule
{
    NameRequired,
    NameTooLong,
    NormalizedNameTooLong,
    SlugInvalid,
    SlugCannotBeGenerated,
    MediaStorageKeyRequired,
    MediaRelativeUrlRequired,
    MediaAltTextRequired,
    MediaRequired,
    IdentityRequired,
    UniverseRequired,
    AuditInstantMustBeUtc,
    AuditActorRequired,
    AuditTimeWentBackwards,
    ReferenceArchived,
    ConcurrencyVersionMismatch,
    ConcurrencyVersionExhausted,
    SeedIdentityDuplicate,
    SeedCodeDuplicate,
    SeedSlugDuplicate,
}
