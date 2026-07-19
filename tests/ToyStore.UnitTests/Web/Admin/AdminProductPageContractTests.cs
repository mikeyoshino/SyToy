namespace ToyStore.UnitTests.Web.Admin;

using ToyStore.Web.Components.Admin.Primitives;

public sealed class AdminProductPageContractTests
{
    [Fact]
    public void ProductFilterBuildsCanonicalUrlForAllCatalogDimensions()
    {
        var category = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var brand = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var universe = Guid.Parse("20000000-0000-0000-0000-000000000001");

        var url = AdminProductFilterBar.BuildCanonicalUrl("/admin/products", new AdminProductFilterQueryState(
            "  สินค้า   หลัก ", "published", category, brand, universe, 2));

        Assert.Equal($"/admin/products?q={Uri.EscapeDataString("สินค้า หลัก")}&status=published&category={category:D}&brand={brand:D}&universe={universe:D}&page=2", url);
    }

    [Fact]
    public void ProductPageComposesAuthorizedThaiManagementWorkflow()
    {
        var source = Source("Components/Admin/Pages/Products.razor");

        Assert.Contains("ManageProductsQuery", source, StringComparison.Ordinal);
        Assert.Contains("CreateInStockProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("UpdateDraftInStockProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("CreatePreOrderProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("UpdateDraftPreOrderProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("PublishProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("ArchiveProductCommand", source, StringComparison.Ordinal);
        Assert.Contains("เพิ่มสินค้าแบบพร้อมส่ง", source, StringComparison.Ordinal);
        Assert.Contains("บันทึกฉบับร่าง", source, StringComparison.Ordinal);
        Assert.Contains("บันทึกการแก้ไข", source, StringComparison.Ordinal);
        Assert.Contains("<AdminProductMediaField", Source("Components/Admin/Shared/AdminProductEditor.razor"), StringComparison.Ordinal);
        Assert.Contains("<AdminProductFilterBar", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"category\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"brand\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"universe\")]", source, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource? loadTokenSource", source, StringComparison.Ordinal);
        Assert.Contains("loadGeneration", source, StringComparison.Ordinal);
        Assert.Contains("CancelCurrentLoadAsync", source, StringComparison.Ordinal);
        Assert.Contains("<StoreSelectField", Source("Components/Admin/Shared/AdminProductEditor.razor"), StringComparison.Ordinal);
        Assert.Contains("<StoreNumberField", Source("Components/Admin/Shared/AdminProductEditor.razor"), StringComparison.Ordinal);
        var list = Source("Components/Admin/Shared/AdminProductList.razor");
        Assert.Contains("item.SaleType == ProductManagementSaleType.PreOrder", list, StringComparison.Ordinal);
        Assert.Contains("ยังไม่รองรับการเก็บพรีออเดอร์", list, StringComparison.Ordinal);
        Assert.Contains("แก้ไขสินค้า", list, StringComparison.Ordinal);
        Assert.Contains("EditRequested.InvokeAsync(item)", list, StringComparison.Ordinal);
        Assert.Contains("IsPublished", Source("Components/Admin/Shared/AdminProductEditor.razor"), StringComparison.Ordinal);
        Assert.Contains("ล็อกหลังเผยแพร่", Source("Components/Admin/Shared/AdminProductEditor.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedMediaControlSupportsPrimaryReorderKeyboardFallbackAndBrowserPreview()
    {
        var source = Source("Components/Admin/Shared/AdminProductMediaField.razor");
        var script = Source("Components/Admin/Shared/AdminProductMediaField.razor.js");

        Assert.Contains("draggable=\"@(!Disabled)\"", source, StringComparison.Ordinal);
        Assert.Contains("@onkeydown", source, StringComparison.Ordinal);
        Assert.Contains("if (Disabled) return;", source, StringComparison.Ordinal);
        Assert.Contains("if (Disabled) { dragIndex = null; return Task.CompletedTask; }", source, StringComparison.Ordinal);
        Assert.Contains("HandleReorderKeyAsync(KeyboardEventArgs args, int index) => Disabled", source, StringComparison.Ordinal);
        Assert.Contains("เลื่อนรูปไปก่อนหน้า", source, StringComparison.Ordinal);
        Assert.Contains("เลื่อนรูปไปถัดไป", source, StringComparison.Ordinal);
        Assert.Contains("ภาพหลัก", source, StringComparison.Ordinal);
        Assert.Contains("URL.createObjectURL", script, StringComparison.Ordinal);
        Assert.Contains("URL.revokeObjectURL", script, StringComparison.Ordinal);
        Assert.Contains("aria-invalid", source, StringComparison.Ordinal);
        Assert.Contains("product-media-error", source, StringComparison.Ordinal);
        Assert.Contains("กรุณาเลือกไฟล์ใหม่", source, StringComparison.Ordinal);
        Assert.Contains("export function clear", script, StringComparison.Ordinal);
        Assert.DoesNotContain("State.Items.Count >= 8 || State.HasPendingSelection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<legend>รูปภาพสินค้า <span", source, StringComparison.Ordinal);
        Assert.Contains("ValidityChanged.InvokeAsync(false)", source, StringComparison.Ordinal);
        Assert.Contains("ExternalErrorMessage", source, StringComparison.Ordinal);

        var editor = Source("Components/Admin/Shared/AdminProductEditor.razor");
        Assert.Contains("!mediaIsValid || !string.IsNullOrWhiteSpace(mediaErrorMessage)", editor, StringComparison.Ordinal);
        Assert.Contains("failure.PropertyName.Equals(\"Images\"", editor, StringComparison.Ordinal);
        Assert.Contains("ExternalErrorMessage=\"@mediaErrorMessage\"", editor, StringComparison.Ordinal);
        Assert.Contains("validationStore!.Display(failures)", editor, StringComparison.Ordinal);
        Assert.Contains("await validationSummary.FocusAsync()", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Where(failure => !failure.PropertyName.Equals(\"Images\"", editor, StringComparison.Ordinal);
        Assert.Contains("? [new FieldValidationFailure(string.Empty, result.Error.Message)]", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalReferenceFiltersAreSeparateFromActiveEditorOptions()
    {
        var page = Source("Components/Admin/Pages/Products.razor");
        var editor = Source("Components/Admin/Shared/AdminProductEditor.razor");
        var reader = SourceFromRepository("src/ToyStore.Infrastructure/Persistence/ProductManagementReader.cs");

        Assert.Contains("BrandFilterOptions", page, StringComparison.Ordinal);
        Assert.Contains("BrandEditorOptions", page, StringComparison.Ordinal);
        Assert.Contains("IncludeCurrentReference", page, StringComparison.Ordinal);
        Assert.Contains("IsActive = false", page, StringComparison.Ordinal);
        Assert.Contains("เก็บแล้ว — กรุณาเลือกใหม่", editor, StringComparison.Ordinal);
        Assert.Contains("Disabled: !x.IsActive", editor, StringComparison.Ordinal);
        Assert.Contains("brandFilterOptions.Where(option => option.IsActive)", reader, StringComparison.Ordinal);
        Assert.Contains("universeFilterOptions.Where(option => option.IsActive)", reader, StringComparison.Ordinal);
    }

    private static string Source(string relativePath) => File.ReadAllText(Path.Combine(Root(), "src", "ToyStore.Web", relativePath));
    private static string SourceFromRepository(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath));
    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
