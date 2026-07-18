using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminBrandPageContractTests
{
    [Fact]
    public void PageOwnsUrlDrivenCancelableBrandUseCasesAndCanonicalNavigation()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Contains("@inject ISender Sender", source, StringComparison.Ordinal);
        Assert.Contains("@inject AdminRequestExecutor RequestExecutor", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"q\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"status\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"page\")]", source, StringComparison.Ordinal);
        Assert.Contains("public string? Page", source, StringComparison.Ordinal);
        Assert.Contains("int.TryParse", source, StringComparison.Ordinal);
        Assert.Contains("NumberStyles.None", source, StringComparison.Ordinal);
        Assert.Contains("ListBrandsQuery", source, StringComparison.Ordinal);
        Assert.Contains("CreateBrandCommand", source, StringComparison.Ordinal);
        Assert.Contains("UpdateBrandCommand", source, StringComparison.Ordinal);
        Assert.Contains("ArchiveBrandCommand", source, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource", source, StringComparison.Ordinal);
        Assert.Contains("loadGeneration", source, StringComparison.Ordinal);
        Assert.Contains("replace: true", source, StringComparison.Ordinal);
        Assert.Contains("AdminFilterBar.BuildCanonicalUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditingLegacyBrandWithoutImageKeepsTheSharedImageFieldRequired()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Contains(
            "ImageRequired=\"@(editingItem is null || string.IsNullOrWhiteSpace(editingItem.ImageUrl))\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ArchivedBrandsDoNotOfferAnEditActionThatTheHandlerWillReject()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Contains("CanArchive: active", source, StringComparison.Ordinal);
        Assert.Contains("CanEdit: active", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalRedirectInvalidatesAndCancelsAnyInFlightBrandLoadFirst()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Matches(
            new Regex(
                @"if \(!CurrentRelativeUrl\(\)\.Equals\(canonicalUrl,[\s\S]*?await CancelCurrentLoadAsync\(\);[\s\S]*?NavigateTo\(canonicalUrl, replace: true\)",
                RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(
                @"if \(page\.PageNumber != requestedState\.Page\)[\s\S]*?await CancelCurrentLoadAsync\(\);[\s\S]*?NavigateTo\(effectiveUrl, replace: true\)",
                RegexOptions.CultureInvariant),
            source);
        Assert.Matches(
            new Regex(
                @"private async Task CancelCurrentLoadAsync\(\)[\s\S]*?Interlocked\.Increment\(ref loadGeneration\)[\s\S]*?Interlocked\.Exchange\(ref loadTokenSource, null\)[\s\S]*?CancelAsync\(\)[\s\S]*?Dispose\(\)",
                RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void MutationSuccessFallsBackToTheHeadingWhenReloadCannotRenderTheAffectedRow()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Contains(
            "contentState != AdminContentState.Ready || items.All(item => item.Id != affectedId)",
            source,
            StringComparison.Ordinal);
        Assert.Equal(2, source.Split("if (ShouldFocusHeader(", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, source.Split("Closed=\"HandleMutationDialogClosedAsync\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("private async Task HandleMutationDialogClosedAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("protected override async Task OnAfterRenderAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PageComposesThaiListStatesSamePageDialogsToastAndFocusFallback()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");
        var layout = Source("Components/Admin/Layout/AdminLayout.razor");

        Assert.Contains("<AdminContentStateView", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceList", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceEditor", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceArchive", source, StringComparison.Ordinal);
        Assert.Contains("<StoreToastHost", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<StoreToast", source, StringComparison.Ordinal);
        Assert.Contains("เพิ่มแบรนด์", source, StringComparison.Ordinal);
        Assert.Contains("แก้ไขแบรนด์", source, StringComparison.Ordinal);
        Assert.Contains("เก็บแบรนด์", source, StringComparison.Ordinal);
        Assert.Contains("ไม่พบแบรนด์ที่ตรงกับตัวกรอง", source, StringComparison.Ordinal);
        Assert.Contains("ProductReferenceCount", source, StringComparison.Ordinal);
        Assert.Contains("Asia/Bangkok", source, StringComparison.Ordinal);
        Assert.Contains("header.FocusAsync", source, StringComparison.Ordinal);
        Assert.Contains("isEditorBusy", source, StringComparison.Ordinal);
        Assert.Contains("isArchiveBusy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UploadTransportAllowsStorageToEnforceFiveMebibytesWithoutUsingDefaultBrowserLimit()
    {
        var source = Source("Components/Admin/Pages/Brands.razor");

        Assert.Contains("5 * 1024 * 1024 + 1", source, StringComparison.Ordinal);
        Assert.Contains("OpenReadStream", source, StringComparison.Ordinal);
        Assert.Contains("MediaStorageErrors.TooLarge", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenReadStream()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedTaskTenComponentsExposeOnlyAdditiveTaskElevenHooks()
    {
        var editor = Source("Components/Admin/Shared/AdminCatalogReferenceEditor.razor");
        var archive = Source("Components/Admin/Shared/AdminCatalogReferenceArchive.razor");
        var header = Source("Components/Admin/Shared/AdminPageHeader.razor");
        var item = Source("Components/Admin/Shared/AdminCatalogReferenceListItem.cs");
        var state = Source("Components/Admin/Shared/AdminContentStateView.razor");

        Assert.Contains("FieldAliases", editor, StringComparison.Ordinal);
        Assert.Contains("ErrorMessage", archive, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", header, StringComparison.Ordinal);
        Assert.Contains("FocusAsync", header, StringComparison.Ordinal);
        Assert.Contains("long Version", item, StringComparison.Ordinal);
        Assert.Contains("LoadingMessage", state, StringComparison.Ordinal);
        Assert.Contains("EmptyTitle", state, StringComparison.Ordinal);
        Assert.Contains("ErrorTitle", state, StringComparison.Ordinal);
    }

    private static string Source(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ToyStore.Web", relativePath));

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
