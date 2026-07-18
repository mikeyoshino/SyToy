using Microsoft.AspNetCore.Components.Forms;

namespace ToyStore.Web.Components.Admin.Primitives;

public sealed class AdminCatalogReferenceEditorModel
{
    public string? DisplayName { get; set; }

    public string? EnglishName { get; set; }

    public string? Slug { get; set; }

    public IBrowserFile? Image { get; set; }
}
