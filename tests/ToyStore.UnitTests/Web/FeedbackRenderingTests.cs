using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ToyStore.Web.Components.Feedback;

namespace ToyStore.UnitTests.Web;

public sealed class FeedbackRenderingTests
{
    [Theory]
    [InlineData(StoreAlertTone.Info, "status")]
    [InlineData(StoreAlertTone.Success, "status")]
    [InlineData(StoreAlertTone.Warning, "status")]
    [InlineData(StoreAlertTone.Error, "alert")]
    public async Task AlertUsesUrgencyAppropriateRole(StoreAlertTone tone, string expectedRole)
    {
        var html = await RenderAsync<StoreAlert>(new()
        {
            [nameof(StoreAlert.Tone)] = tone,
            [nameof(StoreAlert.Title)] = "สถานะ",
            [nameof(StoreAlert.Message)] = "รายละเอียดสถานะ",
        });

        Assert.Contains($"role=\"{expectedRole}\"", html, StringComparison.Ordinal);
        Assert.Contains("สถานะ", html, StringComparison.Ordinal);
        Assert.Contains("รายละเอียดสถานะ", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToastUsesPoliteLiveRegionAndAccessibleDismissButton()
    {
        var html = await RenderAsync<StoreToast>(new()
        {
            [nameof(StoreToast.Message)] = "เพิ่มสินค้าลงตะกร้าแล้ว",
            [nameof(StoreToast.ShowDismiss)] = true,
        });

        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"ปิดข้อความแจ้งเตือน\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToastDismissInvokesTheSuppliedCallbackExactlyOnce()
    {
        var dismissCount = 0;
#pragma warning disable BL0005 // Direct assignment verifies the callback seam used by @onclick.
        var toast = new StoreToast
        {
            Dismissed = EventCallback.Factory.Create(this, () => dismissCount++),
        };
#pragma warning restore BL0005

        await toast.DismissAsync();

        Assert.Equal(1, dismissCount);
    }

    [Fact]
    public async Task SkeletonHidesVisualShapeAndProvidesOptionalThaiStatus()
    {
        var html = await RenderAsync<StoreSkeleton>(new()
        {
            [nameof(StoreSkeleton.StatusText)] = "กำลังโหลดข้อมูล",
        });

        Assert.Contains("aria-hidden=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("กำลังโหลดข้อมูล", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DialogHasNativeAccessibleTitleCloseAndActionRegions()
    {
        var html = await RenderAsync<StoreDialog>(new()
        {
            [nameof(StoreDialog.Title)] = "ยืนยันการทำรายการ",
            [nameof(StoreDialog.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "ตรวจสอบข้อมูลก่อนดำเนินการ")),
            [nameof(StoreDialog.Actions)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<button type=\"button\">ยืนยัน</button>")),
        });

        Assert.Contains("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("role=\"dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-labelledby=\"", html, StringComparison.Ordinal);
        Assert.Contains("ยืนยันการทำรายการ", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"ปิดหน้าต่าง\"", html, StringComparison.Ordinal);
        Assert.Contains("ยืนยัน", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DrawerPlacement.Start, "store-drawer--start")]
    [InlineData(DrawerPlacement.End, "store-drawer--end")]
    public async Task DrawerReusesDialogSemanticsWithPlacementClass(
        DrawerPlacement placement,
        string expectedClass)
    {
        var html = await RenderAsync<StoreDrawer>(new()
        {
            [nameof(StoreDrawer.Title)] = "ตัวกรองสินค้า",
            [nameof(StoreDrawer.Placement)] = placement,
        });

        Assert.Contains("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("role=\"dialog\"", html, StringComparison.Ordinal);
        Assert.Contains(expectedClass, html, StringComparison.Ordinal);
        Assert.Contains("ตัวกรองสินค้า", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
}
