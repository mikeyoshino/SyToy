using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminUniversePageContractTests
{
    [Fact]
    public void PageOwnsAuthorizedUrlDrivenCancelableUniverseUseCases()
    {
        var source = Source("Components/Admin/Pages/Universes.razor");

        Assert.Contains("@page \"/admin/universes\"", source, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanAccessAdmin", source, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanManageProducts", source, StringComparison.Ordinal);
        Assert.Contains("@inject ISender Sender", source, StringComparison.Ordinal);
        Assert.Contains("@inject AdminRequestExecutor RequestExecutor", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"q\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"status\")]", source, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"page\")]", source, StringComparison.Ordinal);
        Assert.Contains("ListUniversesQuery", source, StringComparison.Ordinal);
        Assert.Contains("CreateUniverseCommand", source, StringComparison.Ordinal);
        Assert.Contains("UpdateUniverseCommand", source, StringComparison.Ordinal);
        Assert.Contains("ArchiveUniverseCommand", source, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource", source, StringComparison.Ordinal);
        Assert.Contains("loadGeneration", source, StringComparison.Ordinal);
        Assert.Contains("CancelCurrentLoadAsync", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("await CancelCurrentLoadAsync();", StringComparison.Ordinal)
            < source.IndexOf("NavigationManager.NavigateTo(canonicalUrl, replace: true)", StringComparison.Ordinal));
        Assert.Contains("public string? Page", source, StringComparison.Ordinal);
        Assert.Contains("int.TryParse", source, StringComparison.Ordinal);
        Assert.Contains("NumberStyles.None", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.InvariantCulture", source, StringComparison.Ordinal);
        Assert.Contains("replace: true", source, StringComparison.Ordinal);
        Assert.Contains("AdminFilterBar.BuildCanonicalUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PageComposesThaiStatesCountsSeedReadinessDialogsToastAndFocus()
    {
        var source = Source("Components/Admin/Pages/Universes.razor");
        var layout = Source("Components/Admin/Layout/AdminLayout.razor");

        Assert.Contains("<AdminContentStateView", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceList", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceEditor", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCatalogReferenceArchive", source, StringComparison.Ordinal);
        Assert.Contains("<StoreToastHost", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<StoreToast", source, StringComparison.Ordinal);
        Assert.Contains("เพิ่มจักรวาล", source, StringComparison.Ordinal);
        Assert.Contains("แก้ไขจักรวาล", source, StringComparison.Ordinal);
        Assert.Contains("เก็บจักรวาล", source, StringComparison.Ordinal);
        Assert.Contains("ไม่พบจักรวาลที่ตรงกับตัวกรอง", source, StringComparison.Ordinal);
        Assert.Contains("ต้องเพิ่มโลโก้", source, StringComparison.Ordinal);
        Assert.Contains("เก็บแล้ว — ไม่เปิดให้ใช้งาน", source, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(item.LogoPublicRelativeUrl)", source, StringComparison.Ordinal);
        Assert.Contains("CanEdit: active", source, StringComparison.Ordinal);
        Assert.Contains(
            "editingItem is null || string.IsNullOrWhiteSpace(editingItem.ImageUrl)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("ProductReferenceCount", source, StringComparison.Ordinal);
        Assert.Contains("CharacterReferenceCount", source, StringComparison.Ordinal);
        Assert.Contains("สินค้า", source, StringComparison.Ordinal);
        Assert.Contains("ตัวละคร", source, StringComparison.Ordinal);
        Assert.Contains("Asia/Bangkok", source, StringComparison.Ordinal);
        Assert.Contains("header.FocusAsync", source, StringComparison.Ordinal);
        Assert.Contains("isEditorBusy", source, StringComparison.Ordinal);
        Assert.Contains("isArchiveBusy", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LogoAliasesAndTransportPreserveApplicationFieldErrorsAndStorageLimit()
    {
        var source = Source("Components/Admin/Pages/Universes.razor");

        Assert.Contains("nameof(CreateUniverseCommand.Logo)", source, StringComparison.Ordinal);
        Assert.Contains("nameof(UpdateUniverseCommand.ReplacementLogo)", source, StringComparison.Ordinal);
        Assert.Contains("nameof(AdminCatalogReferenceEditorModel.Image)", source, StringComparison.Ordinal);
        Assert.Contains("5 * 1024 * 1024 + 1", source, StringComparison.Ordinal);
        Assert.Contains("OpenReadStream", source, StringComparison.Ordinal);
        Assert.Contains("MediaStorageErrors.TooLarge", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenReadStream()", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanonicalRedirectInvalidationAdvancesGenerationAndCancelsInFlightLoad()
    {
        var pageType = typeof(ToyStore.Web.Components.Admin.Primitives.AdminPageHeader).Assembly.GetType(
            "ToyStore.Web.Components.Admin.Pages.Universes",
            throwOnError: true)!;
        var page = Activator.CreateInstance(pageType, nonPublic: true)!;
        using var inFlight = new CancellationTokenSource();
        var generation = pageType.GetField(
            "loadGeneration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var loadTokenSource = pageType.GetField(
            "loadTokenSource",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var cancel = pageType.GetMethod(
            "CancelCurrentLoadAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        generation.SetValue(page, 41L);
        loadTokenSource.SetValue(page, inFlight);

        await (Task)cancel.Invoke(page, null)!;

        Assert.Equal(42L, generation.GetValue(page));
        Assert.True(inFlight.IsCancellationRequested);
        Assert.Null(loadTokenSource.GetValue(page));
    }

    [Fact]
    public void BeyondLastPageCanonicalRedirectCancelsTheCurrentLoadBeforeNavigation()
    {
        var source = Source("Components/Admin/Pages/Universes.razor");

        Assert.Matches(
            new Regex(
                @"if \(page\.PageNumber != requestedState\.Page\)[\s\S]*?await CancelCurrentLoadAsync\(\);[\s\S]*?NavigateTo\(effectiveUrl, replace: true\)",
                RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void MutationSuccessFallsBackToTheHeadingWhenReloadCannotRenderTheAffectedRow()
    {
        var source = Source("Components/Admin/Pages/Universes.razor");

        Assert.Contains(
            "contentState != AdminContentState.Ready || items.All(item => item.Id != affectedId)",
            source,
            StringComparison.Ordinal);
        Assert.Equal(2, source.Split("if (ShouldFocusHeader(", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, source.Split("Closed=\"HandleMutationDialogClosedAsync\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("private async Task HandleMutationDialogClosedAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("protected override async Task OnAfterRenderAsync", source, StringComparison.Ordinal);
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
