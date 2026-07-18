using Microsoft.AspNetCore.Components.Forms;
using ToyStore.Application.Characters;
using ToyStore.Application.Products.ManageProducts;

namespace ToyStore.Web.Components.Admin.Primitives;

public sealed class AdminProductEditorModel
{
    public string? DisplayName { get; set; }
    public string? EnglishName { get; set; }
    public string? Description { get; set; }
    public Guid ProductCategoryId { get; set; }
    public Guid BrandId { get; set; }
    public Guid UniverseId { get; set; }
    public decimal? Price { get; set; }
    public int? InitialStock { get; set; }
    public ProductManagementSaleType SaleType { get; set; } = ProductManagementSaleType.InStock;
    public decimal? FullPrice { get; set; }
    public decimal? DepositAmount { get; set; }
    public string? CloseDate { get; set; }
    public int? EstimatedArrivalMonth { get; set; }
    public int? EstimatedArrivalYear { get; set; }
    public int? TotalCapacity { get; set; }
    public int? MaxPerCustomer { get; set; }
    public int? BalancePaymentDays { get; set; } = 7;
    public IReadOnlyList<CharacterOption> Characters { get; set; } = [];
    public ProductMediaEditorState Media { get; } = new();
}

public sealed class ProductMediaEditorState
{
    private readonly List<ProductMediaEditorItem> items = [];
    public IReadOnlyList<ProductMediaEditorItem> Items => items;
    public bool HasPendingSelection { get; private set; }

    public void Load(IEnumerable<ProductManagementImage> images)
    {
        items.Clear();
        items.AddRange(images.OrderBy(image => image.SortOrder).Select(image =>
            ProductMediaEditorItem.Retained(image.Id, image.PublicRelativeUrl, image.AltText)));
        HasPendingSelection = false;
    }

    public void AddFiles(IReadOnlyList<IBrowserFile> files, IReadOnlyList<string> previewUrls)
    {
        var available = Math.Max(0, 8 - items.Count);
        for (var index = 0; index < Math.Min(Math.Min(files.Count, previewUrls.Count), available); index++)
        {
            items.Add(ProductMediaEditorItem.Pending(files[index], previewUrls[index]));
        }
        HasPendingSelection = items.Any(item => item.BrowserFile is not null);
    }

    public void Move(int from, int to)
    {
        if (from < 0 || from >= items.Count || to < 0 || to >= items.Count || from == to) return;
        var item = items[from];
        items.RemoveAt(from);
        items.Insert(to, item);
    }

    public void Remove(int index)
    {
        if (index >= 0 && index < items.Count) items.RemoveAt(index);
        HasPendingSelection = items.Any(item => item.BrowserFile is not null);
    }

    public void RemovePending()
    {
        items.RemoveAll(item => item.BrowserFile is not null);
        HasPendingSelection = false;
    }
}

public sealed record ProductMediaEditorItem(
    Guid? ProductImageId,
    IBrowserFile? BrowserFile,
    string PreviewUrl,
    string AltText)
{
    public static ProductMediaEditorItem Retained(Guid id, string url, string alt) =>
        new(id, null, url, alt);
    public static ProductMediaEditorItem Pending(IBrowserFile file, string previewUrl) =>
        new(null, file, previewUrl, file.Name);
}
