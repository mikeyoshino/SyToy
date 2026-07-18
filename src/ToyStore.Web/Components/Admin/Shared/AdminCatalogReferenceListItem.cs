namespace ToyStore.Web.Components.Admin.Primitives;

public sealed record AdminCatalogReferenceListItem(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Slug,
    string? ImageUrl,
    string ImageAlt,
    string LifecycleStatusText,
    AdminStatusTone LifecycleStatusTone,
    string ReadinessText,
    AdminStatusTone ReadinessTone,
    int ProductCount,
    int? CharacterCount,
    string UpdatedText,
    bool CanArchive,
    long Version = 0,
    bool CanEdit = true,
    string? MissingImageText = null);
