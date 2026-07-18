using System.Linq.Expressions;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Web.Components.Admin.Primitives;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminTask10ComponentTests
{
    private static readonly string[] CatalogReferenceComponentPaths =
    [
        "Components/Admin/Shared/AdminCatalogReferenceEditor.razor",
        "Components/Admin/Shared/AdminCatalogReferenceList.razor",
        "Components/Admin/Shared/AdminCatalogReferenceArchive.razor",
    ];

    [Fact]
    public async Task QueryFilterUsesSharedFieldsInsideOneEditFormAndBuildsCanonicalUrl()
    {
        var historyState = new AdminFilterQueryState(
            "  กันดั้ม เอ็กเซีย  ",
            "active",
            1);

        var html = await RenderAsync<AdminFilterBar>(new()
        {
            [nameof(AdminFilterBar.HistoryState)] = historyState,
            [nameof(AdminFilterBar.BasePath)] = "/admin/brands",
        });

        Assert.Equal(1, Count(html, "<form"));
        Assert.Contains("method=\"get\"", html, StringComparison.Ordinal);
        Assert.Contains("ค้นหา", html, StringComparison.Ordinal);
        Assert.Contains("สถานะ", html, StringComparison.Ordinal);
        Assert.Matches("""class="store-select(?:\s|")""", html);
        Assert.Contains("aria-haspopup=\"listbox\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html[(html.IndexOf("<form", StringComparison.Ordinal) + 5)..], StringComparison.Ordinal);
        Assert.Equal(
            "/admin/brands?q=%E0%B8%81%E0%B8%B1%E0%B8%99%E0%B8%94%E0%B8%B1%E0%B9%89%E0%B8%A1%20%E0%B9%80%E0%B8%AD%E0%B9%87%E0%B8%81%E0%B9%80%E0%B8%8B%E0%B8%B5%E0%B8%A2",
            AdminFilterBar.BuildCanonicalUrl("/admin/brands", historyState));

        historyState = historyState with { Status = "archived", Page = 3 };
        Assert.Equal(
            "/admin/brands?q=%E0%B8%81%E0%B8%B1%E0%B8%99%E0%B8%94%E0%B8%B1%E0%B9%89%E0%B8%A1%20%E0%B9%80%E0%B8%AD%E0%B9%87%E0%B8%81%E0%B9%80%E0%B8%8B%E0%B8%B5%E0%B8%A2&status=archived&page=3",
            AdminFilterBar.BuildCanonicalUrl("/admin/brands", historyState));
        Assert.Equal(
            "/admin/brands?q=%E0%B8%81%E0%B8%B1%E0%B8%99%E0%B8%94%E0%B8%B1%E0%B9%89%E0%B8%A1%20%E0%B9%80%E0%B8%AD%E0%B9%87%E0%B8%81%E0%B9%80%E0%B8%8B%E0%B8%B5%E0%B8%A2&status=archived",
            AdminFilterBar.BuildSubmitUrl("/admin/brands", historyState));
    }

    [Fact]
    public void PageOwnedHistoryStateRestoresControlsAcrossBackAndForwardNavigation()
    {
        var editState = new AdminFilterEditState();
        var initial = new AdminFilterQueryState(null, "active", 1);
        var forward = new AdminFilterQueryState("กันดั้ม", "archived", 3);

        Assert.True(editState.Restore(initial));
        Assert.True(editState.Restore(forward));
        Assert.Equal("กันดั้ม", editState.Model.Search);
        Assert.Equal("archived", editState.Model.Status);
        Assert.Equal(3, editState.Model.Page);

        editState.Model.Search = "ค่าที่ยังไม่ได้ส่ง";
        Assert.True(editState.Restore(initial));
        Assert.Null(editState.Model.Search);
        Assert.Equal("active", editState.Model.Status);
        Assert.Equal(1, editState.Model.Page);

        Assert.True(editState.Restore(forward));
        Assert.Equal("กันดั้ม", editState.Model.Search);
        Assert.Equal("archived", editState.Model.Status);
        Assert.Equal(3, editState.Model.Page);
        Assert.False(editState.Restore(forward));
    }

    [Fact]
    public async Task EmptyStateSupportsCallbackAndCustomFragmentWithoutInventedRoute()
    {
        var callbackHtml = await RenderAsync<AdminContentStateView>(new()
        {
            [nameof(AdminContentStateView.State)] = AdminContentState.Empty,
            [nameof(AdminContentStateView.EmptyActionLabel)] = "เพิ่มแบรนด์",
            [nameof(AdminContentStateView.EmptyActionClicked)] = EventCallback.Factory.Create(this, () => { }),
        });
        var fragmentHtml = await RenderAsync<AdminContentStateView>(new()
        {
            [nameof(AdminContentStateView.State)] = AdminContentState.Empty,
            [nameof(AdminContentStateView.EmptyAction)] = Markup("<button type=\"button\">ล้างตัวกรอง</button>"),
        });

        Assert.Contains("<button", callbackHtml, StringComparison.Ordinal);
        Assert.Contains("เพิ่มแบรนด์", callbackHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", callbackHtml, StringComparison.Ordinal);
        Assert.Contains("ล้างตัวกรอง", fragmentHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SingleImageFieldUsesLocalPreviewContractAndThaiAccessibleHelp()
    {
        var model = new ImageModel();
        var editContext = new EditContext(model);
        Expression<Func<IBrowserFile?>> expression = () => model.Image;

        var html = await RenderInsideEditContextAsync<StoreSingleImageField>(editContext, new()
        {
            [nameof(StoreSingleImageField.Label)] = "รูปภาพแบรนด์",
            [nameof(StoreSingleImageField.Value)] = model.Image,
            [nameof(StoreSingleImageField.ValueChanged)] = EventCallback.Factory.Create<IBrowserFile?>(this, value => model.Image = value),
            [nameof(StoreSingleImageField.ValueExpression)] = expression,
            [nameof(StoreSingleImageField.CurrentImageUrl)] = "/media/current/brand.webp",
            [nameof(StoreSingleImageField.CurrentImageAlt)] = "แบรนด์ปัจจุบัน",
            [nameof(StoreSingleImageField.Required)] = true,
        });

        Assert.Contains("type=\"file\"", html, StringComparison.Ordinal);
        Assert.Contains("accept=\"image/jpeg,image/png,image/webp\"", html, StringComparison.Ordinal);
        Assert.Contains("JPEG, PNG หรือ WebP ไม่เกิน 5 MB", html, StringComparison.Ordinal);
        Assert.Contains("เลือกไฟล์", html, StringComparison.Ordinal);
        Assert.Contains("/media/current/brand.webp", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"แบรนด์ปัจจุบัน\"", html, StringComparison.Ordinal);
        Assert.Matches("<input(?=[^>]*type=\"file\")(?=[^>]*tabindex=\"-1\")(?=[^>]*aria-hidden=\"true\")[^>]*>", html);
        Assert.Matches("<button(?=[^>]*class=\"store-image-field__picker\")(?=[^>]*aria-labelledby=\"[^\"]+-label [^\"]+-picker-text\")(?=[^>]*aria-describedby=\"[^\"]+-help [^\"]+-error\")[^>]*>", html);
        Assert.DoesNotContain("data:image", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParentResetOfSameModelRequestsPreviewRevocationExactlyOnce()
    {
        var state = new SingleImagePreviewState();
        var selected = new TestBrowserFile();

        Assert.False(state.ObserveValue(null));
        Assert.False(state.ObserveValue(selected));
        state.SetPreview("blob:brand-preview");

        Assert.True(state.ObserveValue(null));
        Assert.Null(state.PreviewUrl);
        Assert.False(state.ObserveValue(null));
    }

    [Fact]
    public async Task SharedEditorListAndArchiveComposeExistingPrimitivesOnly()
    {
        var model = new AdminCatalogReferenceEditorModel
        {
            DisplayName = "บันได",
            EnglishName = "Bandai",
            Slug = "bandai",
        };
        var editor = await RenderAsync<AdminCatalogReferenceEditor>(new()
        {
            [nameof(AdminCatalogReferenceEditor.Model)] = model,
            [nameof(AdminCatalogReferenceEditor.ImageLabel)] = "รูปภาพแบรนด์",
        });
        var item = new AdminCatalogReferenceListItem(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "บันได",
            "Bandai",
            "bandai",
            "/media/brand.webp",
            "โลโก้แบรนด์ บันได",
            "ใช้งาน",
            AdminStatusTone.Info,
            "ต้องเพิ่มโลโก้",
            AdminStatusTone.Warning,
            4,
            2,
            "17 ก.ค. 2569 10:30",
            true);
        var list = await RenderAsync<AdminCatalogReferenceList>(new()
        {
            [nameof(AdminCatalogReferenceList.Caption)] = "รายการแบรนด์",
            [nameof(AdminCatalogReferenceList.Items)] = new[] { item },
            [nameof(AdminCatalogReferenceList.ShowCharacterCount)] = true,
            [nameof(AdminCatalogReferenceList.EntityLabel)] = "แบรนด์",
            [nameof(AdminCatalogReferenceList.EditRequested)] = EventCallback.Factory.Create<AdminCatalogReferenceListItem>(this, _ => { }),
            [nameof(AdminCatalogReferenceList.ArchiveRequested)] = EventCallback.Factory.Create<AdminCatalogReferenceListItem>(this, _ => { }),
            [nameof(AdminCatalogReferenceList.CurrentPage)] = 1,
            [nameof(AdminCatalogReferenceList.TotalPages)] = 2,
            [nameof(AdminCatalogReferenceList.PageUrl)] = (Func<int, string>)(page => $"/admin/brands?page={page}"),
        });
        var archive = await RenderAsync<AdminCatalogReferenceArchive>(new()
        {
            [nameof(AdminCatalogReferenceArchive.IsOpen)] = true,
            [nameof(AdminCatalogReferenceArchive.Title)] = "เก็บแบรนด์",
            [nameof(AdminCatalogReferenceArchive.Description)] = "แบรนด์นี้ถูกใช้งานกับสินค้า 2 รายการ",
            [nameof(AdminCatalogReferenceArchive.IsBusy)] = true,
        });

        Assert.Contains("StoreValidationSummary", Source("Components/Admin/Shared/AdminCatalogReferenceEditor.razor"), StringComparison.Ordinal);
        Assert.Contains("ส่วน URL (slug)", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"bandai\"", editor, StringComparison.Ordinal);
        Assert.Contains("รายการแบรนด์", list, StringComparison.Ordinal);
        Assert.Contains("ใช้งาน", list, StringComparison.Ordinal);
        Assert.Contains("ต้องเพิ่มโลโก้", list, StringComparison.Ordinal);
        Assert.Contains("สถานะวงจรชีวิต: </span>", list, StringComparison.Ordinal);
        Assert.Contains("ความพร้อม: </span>", list, StringComparison.Ordinal);
        Assert.Contains("สินค้า 4 รายการ", list, StringComparison.Ordinal);
        Assert.Contains("ตัวละคร 2 รายการ", list, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"แก้ไขแบรนด์ บันได\"", list, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"เก็บแบรนด์ บันได\"", list, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"การแบ่งหน้า\"", list, StringComparison.Ordinal);
        Assert.Contains("data-admin-archive-cancel", archive, StringComparison.Ordinal);
        Assert.Contains("disabled", archive, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerminalCatalogReferenceHidesEditWhileCanEditDefaultsToTrue()
    {
        var active = Item("ใช้งาน");
        var archived = Item("เก็บแล้ว") with { CanEdit = false, CanArchive = false };

        var activeHtml = await RenderListAsync(active);
        var archivedHtml = await RenderListAsync(archived);

        Assert.True(active.CanEdit);
        Assert.Contains("aria-label=\"แก้ไขจักรวาล จักรวาลทดสอบ\"", activeHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"แก้ไขจักรวาล จักรวาลทดสอบ\"", archivedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"เก็บจักรวาล จักรวาลทดสอบ\"", archivedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerminalCatalogReferenceCanReplaceImperativeMissingImageCopyWithNeutralCopy()
    {
        var archived = Item("เก็บแล้ว") with
        {
            ImageUrl = null,
            CanEdit = false,
            CanArchive = false,
            MissingImageText = "ไม่มีโลโก้",
        };

        var html = await RenderListAsync(archived, missingImageText: "ต้องเพิ่มโลโก้");

        Assert.Contains("ไม่มีโลโก้", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ต้องเพิ่มโลโก้", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BlobPreviewScriptCreatesAndRevokesObjectUrlsWithoutReadingOrUploadingBytes()
    {
        var script = Source("Components/Forms/StoreSingleImageField.razor.js");

        Assert.Contains("URL.createObjectURL", script, StringComparison.Ordinal);
        Assert.Contains("URL.revokeObjectURL", script, StringComparison.Ordinal);
        Assert.Contains("WeakMap", script, StringComparison.Ordinal);
        Assert.Contains("addEventListener(\"close\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("FileReader", script, StringComparison.Ordinal);
        Assert.DoesNotContain("readAsDataURL", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("XMLHttpRequest", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedDialogScriptBlocksNativeEscapeWhileBusyAndSupportsInitialFocus()
    {
        var script = Source("Components/Feedback/StoreDialog.razor.js");
        var dialog = Source("Components/Feedback/StoreDialog.razor");

        Assert.Contains("addEventListener(\"cancel\"", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault()", script, StringComparison.Ordinal);
        Assert.Contains("data-dismissible=\"@Dismissible.ToString().ToLowerInvariant()\"", dialog, StringComparison.Ordinal);
        Assert.Contains("dialog.dataset.dismissible === \"false\"", script, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener(\"cancel\", preventBusyDialogCancel, true)", script, StringComparison.Ordinal);
        Assert.Contains("event.key === \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains("dialog[open][data-dismissible=\"false\"]", script, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener(\"keydown\", preventBusyDialogEscape, true)", script, StringComparison.Ordinal);
        Assert.Contains("export function setDismissible", script, StringComparison.Ordinal);
        Assert.Contains("dialog.querySelector(initialFocusSelector)?.focus()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoritativeMediaFailureMapsToImageAndDoesNotMutateSelectedFile()
    {
        var selected = new TestBrowserFile();
        var model = new AdminCatalogReferenceEditorModel { Image = selected };

        var failures = AdminCatalogReferenceEditor.MapFailures(
            Result.Failure(MediaStorageErrors.TooLarge));

        var failure = Assert.Single(failures);
        Assert.Equal(nameof(AdminCatalogReferenceEditorModel.Image), failure.PropertyName);
        Assert.Equal(MediaStorageErrors.TooLarge.Message, failure.ErrorMessage);
        Assert.Same(selected, model.Image);
    }

    [Fact]
    public void TaskElevenFieldAliasMapsUpdateMediaFailureToTheSharedImageField()
    {
        var failures = AdminCatalogReferenceEditor.MapFailures(
            Result.Failure(
                MediaStorageErrors.TooLarge,
                [new FieldValidationFailure("ReplacementImage", MediaStorageErrors.TooLarge.Message)]),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ReplacementImage"] = nameof(AdminCatalogReferenceEditorModel.Image),
            });

        var failure = Assert.Single(failures);
        Assert.Equal(nameof(AdminCatalogReferenceEditorModel.Image), failure.PropertyName);
        Assert.Equal(MediaStorageErrors.TooLarge.Message, failure.ErrorMessage);
    }

    [Fact]
    public async Task TaskElevenHooksRenderBrandSpecificStateArchiveErrorAndFocusableHeading()
    {
        var state = await RenderAsync<AdminContentStateView>(new()
        {
            [nameof(AdminContentStateView.State)] = AdminContentState.Empty,
            [nameof(AdminContentStateView.EmptyTitle)] = "ยังไม่มีแบรนด์",
            [nameof(AdminContentStateView.EmptyMessage)] = "เพิ่มแบรนด์แรก",
        });
        var archive = await RenderAsync<AdminCatalogReferenceArchive>(new()
        {
            [nameof(AdminCatalogReferenceArchive.IsOpen)] = true,
            [nameof(AdminCatalogReferenceArchive.Title)] = "เก็บแบรนด์",
            [nameof(AdminCatalogReferenceArchive.Description)] = "ยืนยันการเก็บแบรนด์",
            [nameof(AdminCatalogReferenceArchive.ErrorMessage)] = "ข้อมูลถูกแก้ไข กรุณาลองใหม่",
        });
        var header = await RenderAsync<AdminPageHeader>(new()
        {
            [nameof(AdminPageHeader.Title)] = "แบรนด์",
        });

        Assert.Contains("ยังไม่มีแบรนด์", state, StringComparison.Ordinal);
        Assert.Contains("เพิ่มแบรนด์แรก", state, StringComparison.Ordinal);
        Assert.Contains("ไม่สามารถเก็บรายการได้", archive, StringComparison.Ordinal);
        Assert.Contains("ข้อมูลถูกแก้ไข กรุณาลองใหม่", archive, StringComparison.Ordinal);
        Assert.Matches("<h1(?=[^>]*tabindex=\"-1\")[^>]*>แบรนด์</h1>", header);
    }

    [Fact]
    public void CatalogReferenceComponentsStayPresentationOnly()
    {
        var source = string.Join(
            Environment.NewLine,
            CatalogReferenceComponentPaths.Select(Source));

        Assert.DoesNotContain("ToyStore.Domain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog", source, StringComparison.Ordinal);
        Assert.Contains("<AdminModal", source, StringComparison.Ordinal);
        Assert.Contains("<AdminDataTable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchiveCancelDelegatesToTheSharedDialogClosePathOnly()
    {
        var archive = Source("Components/Admin/Shared/AdminCatalogReferenceArchive.razor");
        var modal = Source("Components/Admin/Shared/AdminModal.razor");

        Assert.Contains("modal.RequestCloseAsync", archive, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpenChanged.InvokeAsync(false)", archive, StringComparison.Ordinal);
        Assert.Contains("dialog?.RequestCloseAsync", modal, StringComparison.Ordinal);
        Assert.Contains("Closed=\"@Closed\"", archive, StringComparison.Ordinal);
        Assert.Contains("public EventCallback Closed", archive, StringComparison.Ordinal);
    }

    private static RenderFragment Markup(string markup) => builder => builder.AddMarkupContent(0, markup);

    private static int Count(string value, string text) =>
        value.Split(text, StringSplitOptions.None).Length - 1;

    private static AdminCatalogReferenceListItem Item(string lifecycleStatus) => new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "จักรวาลทดสอบ",
        "Test Universe",
        "test-universe",
        "/media/universe.webp",
        "โลโก้จักรวาลทดสอบ",
        lifecycleStatus,
        AdminStatusTone.Neutral,
        "พร้อมใช้งาน",
        AdminStatusTone.Success,
        0,
        0,
        "17 ก.ค. 2569 10:30",
        true);

    private async Task<string> RenderListAsync(
        AdminCatalogReferenceListItem item,
        string missingImageText = "ยังไม่มีภาพ") =>
        await RenderAsync<AdminCatalogReferenceList>(new()
        {
            [nameof(AdminCatalogReferenceList.Caption)] = "รายการจักรวาล",
            [nameof(AdminCatalogReferenceList.Items)] = new[] { item },
            [nameof(AdminCatalogReferenceList.EntityLabel)] = "จักรวาล",
            [nameof(AdminCatalogReferenceList.MissingImageText)] = missingImageText,
            [nameof(AdminCatalogReferenceList.EditRequested)] =
                EventCallback.Factory.Create<AdminCatalogReferenceListItem>(this, _ => { }),
            [nameof(AdminCatalogReferenceList.ArchiveRequested)] =
                EventCallback.Factory.Create<AdminCatalogReferenceListItem>(this, _ => { }),
            [nameof(AdminCatalogReferenceList.PageUrl)] =
                (Func<int, string>)(page => $"/admin/universes?page={page}"),
        });

    private static string Source(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ToyStore.Web", relativePath));

    private static async Task<string> RenderInsideEditContextAsync<TComponent>(
        EditContext editContext,
        Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        RenderFragment child = builder =>
        {
            builder.OpenComponent<TComponent>(0);
            builder.AddMultipleAttributes(1, parameters.Select(pair =>
                new KeyValuePair<string, object>(pair.Key, pair.Value!)));
            builder.CloseComponent();
        };

        return await RenderAsync<CascadingValue<EditContext>>(new()
        {
            [nameof(CascadingValue<EditContext>.Value)] = editContext,
            [nameof(CascadingValue<EditContext>.IsFixed)] = true,
            [nameof(CascadingValue<EditContext>.ChildContent)] = child,
        });
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private sealed class ImageModel
    {
        public IBrowserFile? Image { get; set; }
    }

    private sealed class TestBrowserFile : IBrowserFile
    {
        public string Name => "too-large.webp";

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Size => 6 * 1024 * 1024;

        public string ContentType => "image/webp";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The shared UI test must not open or upload file bytes.");
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/admin/brands");

        protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }
}
