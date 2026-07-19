using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ToyStore.Web.Components.Admin.Navigation;
using ToyStore.Web.Components.Admin.Primitives;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminPrimitivesRenderingTests
{
    [Fact]
    public async Task PageHeaderOwnsOneHeadingDescriptionAndOptionalActions()
    {
        var html = await RenderAsync<AdminPageHeader>(new()
        {
            [nameof(AdminPageHeader.Title)] = "สินค้า",
            [nameof(AdminPageHeader.Description)] = "จัดการข้อมูลสินค้าของร้าน",
            [nameof(AdminPageHeader.Actions)] = Markup("<button type=\"button\">เพิ่มสินค้า</button>"),
        });

        Assert.Equal(1, Count(html, "<h1"));
        Assert.Contains("สินค้า", html, StringComparison.Ordinal);
        Assert.Contains("จัดการข้อมูลสินค้าของร้าน", html, StringComparison.Ordinal);
        Assert.Contains("เพิ่มสินค้า", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AdminStatusTone.Neutral, "admin-status-badge--neutral")]
    [InlineData(AdminStatusTone.Info, "admin-status-badge--info")]
    [InlineData(AdminStatusTone.Success, "admin-status-badge--success")]
    [InlineData(AdminStatusTone.Warning, "admin-status-badge--warning")]
    [InlineData(AdminStatusTone.Danger, "admin-status-badge--danger")]
    public async Task StatusBadgeAlwaysIncludesVisibleTextAndIcon(
        AdminStatusTone tone,
        string expectedClass)
    {
        var html = await RenderAsync<AdminStatusBadge>(new()
        {
            [nameof(AdminStatusBadge.Text)] = "พร้อมดำเนินการ",
            [nameof(AdminStatusBadge.Tone)] = tone,
        });

        Assert.Contains(expectedClass, html, StringComparison.Ordinal);
        Assert.Contains("admin-status-badge__icon", html, StringComparison.Ordinal);
        Assert.Contains("พร้อมดำเนินการ", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AdminContentState.Loading, "กำลังโหลดข้อมูล")]
    [InlineData(AdminContentState.Empty, "ยังไม่มีข้อมูล")]
    [InlineData(AdminContentState.Error, "ไม่สามารถโหลดข้อมูลได้")]
    [InlineData(AdminContentState.Ready, "เนื้อหาพร้อมใช้งาน")]
    public async Task ContentStateRendersThaiSemanticState(
        AdminContentState state,
        string expectedText)
    {
        var html = await RenderAsync<AdminContentStateView>(new()
        {
            [nameof(AdminContentStateView.State)] = state,
            [nameof(AdminContentStateView.EmptyActionLabel)] = "เพิ่มรายการแรก",
            [nameof(AdminContentStateView.EmptyActionHref)] = "/admin/items/new",
            [nameof(AdminContentStateView.Retry)] = EventCallback.Factory.Create(this, () => { }),
            [nameof(AdminContentStateView.ReadyContent)] = Markup("<p>เนื้อหาพร้อมใช้งาน</p>"),
        });

        Assert.Contains(expectedText, html, StringComparison.Ordinal);
        if (state == AdminContentState.Loading)
        {
            Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        }
        else if (state == AdminContentState.Empty)
        {
            Assert.Contains("เพิ่มรายการแรก", html, StringComparison.Ordinal);
        }
        else if (state == AdminContentState.Error)
        {
            Assert.Contains("ลองอีกครั้ง", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DataTableRequiresCaptionAndComposesCallerFragmentsInsideLocalScroller()
    {
        var html = await RenderAsync<AdminDataTable>(new()
        {
            [nameof(AdminDataTable.Caption)] = "รายการสินค้า",
            [nameof(AdminDataTable.Header)] = Markup("<tr><th scope=\"col\">ชื่อ</th></tr>"),
            [nameof(AdminDataTable.Rows)] = Markup("<tr><td>หุ่นยนต์รุ่นหนึ่ง</td></tr>"),
        });

        Assert.Contains("admin-data-table__scroller", html, StringComparison.Ordinal);
        Assert.Matches("<caption[^>]*>รายการสินค้า</caption>", html);
        Assert.Matches("<thead[^>]*>", html);
        Assert.Matches("<tbody[^>]*>", html);
        Assert.Contains("หุ่นยนต์รุ่นหนึ่ง", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilterBarUsesGetFormThaiLandmarkSlotsAndOptionalClearLink()
    {
        var html = await RenderAsync<AdminFilterBar>(new()
        {
            [nameof(AdminFilterBar.ChildContent)] = Markup("<label>ค้นหา <input name=\"q\"></label>"),
            [nameof(AdminFilterBar.ClearHref)] = "/admin/products",
        });

        Assert.Contains("method=\"get\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"search\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"ตัวกรองข้อมูล\"", html, StringComparison.Ordinal);
        Assert.Contains("ล้างตัวกรอง", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 12, true, false)]
    [InlineData(6, 12, false, false)]
    [InlineData(12, 12, false, true)]
    public async Task PaginationHasThaiLabelCurrentPageSafeBoundariesAndCompactEllipsis(
        int current,
        int total,
        bool firstDisabled,
        bool lastDisabled)
    {
        var html = await RenderAsync<AdminPagination>(new()
        {
            [nameof(AdminPagination.CurrentPage)] = current,
            [nameof(AdminPagination.TotalPages)] = total,
            [nameof(AdminPagination.PageUrl)] = (Func<int, string>)(page => $"/admin/products?page={page}"),
        });

        Assert.Contains("aria-label=\"การแบ่งหน้า\"", html, StringComparison.Ordinal);
        Assert.Matches(
            $"<span[^>]*aria-current=\"page\"[^>]*>{current}</span>",
            html);
        Assert.Contains("admin-pagination__ellipsis", html, StringComparison.Ordinal);
        Assert.Equal(firstDisabled, html.Contains("ไปหน้าแรก\" aria-disabled=\"true", StringComparison.Ordinal));
        Assert.Equal(lastDisabled, html.Contains("ไปหน้าสุดท้าย\" aria-disabled=\"true", StringComparison.Ordinal));
        Assert.DoesNotContain("page=0", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"page={total + 1}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminModalWrapsTheOneSharedDialog()
    {
        var html = await RenderAsync<AdminModal>(new()
        {
            [nameof(AdminModal.Title)] = "เพิ่มสินค้า",
            [nameof(AdminModal.IsOpen)] = true,
            [nameof(AdminModal.ChildContent)] = Markup("<p>แบบฟอร์มสินค้า</p>"),
        });

        Assert.Contains("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("admin-modal", html, StringComparison.Ordinal);
        Assert.Contains("เพิ่มสินค้า", html, StringComparison.Ordinal);
        Assert.Contains("แบบฟอร์มสินค้า", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TallAdminModalReservesVerticalSpaceForDropdowns()
    {
        var html = await RenderAsync<AdminModal>(new()
        {
            [nameof(AdminModal.Title)] = "เพิ่มข้อมูลจัดส่ง",
            [nameof(AdminModal.IsOpen)] = true,
            [nameof(AdminModal.IsTall)] = true,
            [nameof(AdminModal.ChildContent)] = Markup("<p>บริษัทขนส่ง</p>"),
        });

        Assert.Contains("admin-modal--tall", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin/orders?type=pre-order&search=robot", 1)]
    [InlineData("/admin/orders?type=pre-order&status=shipped", 0)]
    [InlineData("/admin/orders?type=pre-order&type=pre-order", 0)]
    public async Task OrderContextRendersAtMostOneDeterministicCurrentShortcut(
        string currentUrl,
        int expectedCurrentCount)
    {
        var html = await RenderAsync<AdminContextNav>(new()
        {
            [nameof(AdminContextNav.Label)] = "ตัวกรองคำสั่งซื้อ",
            [nameof(AdminContextNav.CurrentUrl)] = currentUrl,
            [nameof(AdminContextNav.Items)] = AdminNavigation.OrderContextItems,
        });

        Assert.Equal(expectedCurrentCount, Count(html, "aria-current=\"page\""));
    }

    [Fact]
    public void AdminPrimitivesRemainPresentationOnlyAndReuseSharedDialog()
    {
        var root = Path.Combine(
            RepositoryRoot(),
            "src",
            "ToyStore.Web",
            "Components",
            "Admin",
            "Shared");
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.DoesNotContain("ToyStore.Domain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.Contains("<StoreDialog", File.ReadAllText(Path.Combine(root, "AdminModal.razor")), StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog", File.ReadAllText(Path.Combine(root, "AdminModal.razor")), StringComparison.Ordinal);
    }

    private static RenderFragment Markup(string markup) => builder => builder.AddMarkupContent(0, markup);

    private static int Count(string value, string text) =>
        value.Split(text, StringSplitOptions.None).Length - 1;

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            provider,
            provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

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

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() =>
            Initialize("https://example.test/", "https://example.test/admin/products");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }
}
