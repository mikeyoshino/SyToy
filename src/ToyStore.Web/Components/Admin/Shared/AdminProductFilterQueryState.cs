namespace ToyStore.Web.Components.Admin.Primitives;

public sealed record AdminProductFilterQueryState(
    string? Search = null,
    string Status = "active",
    Guid? ProductCategoryId = null,
    Guid? BrandId = null,
    Guid? UniverseId = null,
    int Page = 1);
