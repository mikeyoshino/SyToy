using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ToyStore.Web.Components.Feedback;

namespace ToyStore.UnitTests.Web;

public sealed class StoreDialogBehaviorTests
{
    [Fact]
    public async Task InitialOpenImportsInitializesAndShowsTheNativeDialog()
    {
        var module = new RecordingJsModule();
        var runtime = new RecordingJsRuntime(module);
        var dialog = CreateDialog(runtime);
        dialog.ReceiveOpenState(true);

        await dialog.SynchronizeDialogAsync();

        Assert.Equal(1, runtime.ImportCount);
        Assert.Contains(
            module.Calls,
            call => call.Identifier == "initialize"
                && call.Arguments.Length == 2
                && call.Arguments[0] is ElementReference
                && call.Arguments[1] is not null);
        Assert.Contains(
            module.Calls,
            call => call.Identifier == "setDismissible"
                && call.Arguments.Length == 2
                && call.Arguments[1] is true);
        Assert.Contains(
            module.Calls,
            call => call.Identifier == "show"
                && Assert.Single(call.Arguments) is ElementReference);

        await dialog.DisposeAsync();
    }

    [Fact]
    public async Task RazorCloseRequestsNativeCloseAndNotifiesCallbacksOnce()
    {
        var module = new RecordingJsModule();
        var runtime = new RecordingJsRuntime(module);
        var changedCount = 0;
        var closedCount = 0;
        var changedValue = true;
        var dialog = CreateDialog(
            runtime,
            value =>
            {
                changedCount++;
                changedValue = value;
            },
            () => closedCount++);
        dialog.ReceiveOpenState(true);
        await dialog.SynchronizeDialogAsync();

        await dialog.RequestCloseAsync();
        await dialog.HandleNativeClosedAsync();

        Assert.Contains(module.Calls, call => call.Identifier == "close");
        Assert.Equal(1, changedCount);
        Assert.False(changedValue);
        Assert.Equal(1, closedCount);

        await dialog.DisposeAsync();
    }

    [Fact]
    public async Task NativeEscapeCloseAndRepeatedNotificationFireCallbacksOnlyOnce()
    {
        var module = new RecordingJsModule();
        var changedCount = 0;
        var closedCount = 0;
        var dialog = CreateDialog(
            new RecordingJsRuntime(module),
            _ => changedCount++,
            () => closedCount++);
        dialog.ReceiveOpenState(true);
        await dialog.SynchronizeDialogAsync();

        await dialog.HandleNativeClosedAsync();
        await dialog.HandleNativeClosedAsync();

        Assert.Equal(1, changedCount);
        Assert.Equal(1, closedCount);
        Assert.DoesNotContain(module.Calls, call => call.Identifier == "close");

        await dialog.DisposeAsync();
    }

    [Fact]
    public async Task NavigationCloseSuppressesOldTriggerFocusReturnAndNotifiesOnce()
    {
        var module = new RecordingJsModule();
        var changedCount = 0;
        var dialog = CreateDialog(
            new RecordingJsRuntime(module),
            _ => changedCount++);
        dialog.ReceiveOpenState(true);
        await dialog.SynchronizeDialogAsync();

        await dialog.CloseWithoutFocusReturnAsync();
        await dialog.HandleNativeClosedAsync();

        Assert.Contains(module.Calls, call => call.Identifier == "closeWithoutFocusReturn");
        Assert.Equal(1, changedCount);

        await dialog.DisposeAsync();
    }

    [Fact]
    public async Task BusyDialogRejectsRazorDismissAndConfiguresNativeEscapeBlock()
    {
        var module = new RecordingJsModule();
        var changedCount = 0;
        var dialog = CreateDialog(
            new RecordingJsRuntime(module),
            _ => changedCount++);
#pragma warning disable BL0005 // Direct assignment exercises the shared busy seam.
        dialog.Dismissible = false;
#pragma warning restore BL0005
        dialog.ReceiveOpenState(true);

        await dialog.SynchronizeDialogAsync();
        await dialog.RequestCloseAsync();

        Assert.Contains(
            module.Calls,
            call => call.Identifier == "setDismissible"
                && call.Arguments.Length == 2
                && Assert.IsType<bool>(call.Arguments[1]) is false);
        Assert.DoesNotContain(module.Calls, call => call.Identifier == "close");
        Assert.Equal(0, changedCount);

        await dialog.DisposeAsync();
    }

    [Fact]
    public async Task DisposalCleansUpTheModuleAndIsIdempotent()
    {
        var module = new RecordingJsModule();
        var dialog = CreateDialog(new RecordingJsRuntime(module));
        dialog.ReceiveOpenState(true);
        await dialog.SynchronizeDialogAsync();

        await dialog.DisposeAsync();
        await dialog.DisposeAsync();

        Assert.Equal(1, module.Calls.Count(call => call.Identifier == "dispose"));
        Assert.Equal(1, module.DisposeCount);
    }

    [Theory]
    [InlineData(FailureKind.Disconnected)]
    [InlineData(FailureKind.ObjectDisposed)]
    public async Task DisposalToleratesDisconnectedOrDisposedInterop(FailureKind failureKind)
    {
        var module = new RecordingJsModule();
        var dialog = CreateDialog(new RecordingJsRuntime(module));
        dialog.ReceiveOpenState(true);
        await dialog.SynchronizeDialogAsync();
        module.Failure = failureKind;

        var exception = await Record.ExceptionAsync(async () => await dialog.DisposeAsync());

        Assert.Null(exception);
        Assert.Equal(1, module.DisposeCount);
    }

    private static StoreDialog CreateDialog(
        IJSRuntime runtime,
        Action<bool>? changed = null,
        Action? closed = null)
    {
#pragma warning disable BL0005 // Direct assignment exercises internal lifecycle without a browser renderer.
        return new StoreDialog
        {
            JsRuntime = runtime,
            Title = "ทดสอบหน้าต่าง",
            IsOpenChanged = EventCallback.Factory.Create<bool>(new object(), changed ?? (_ => { })),
            Closed = EventCallback.Factory.Create(new object(), closed ?? (() => { })),
        };
#pragma warning restore BL0005
    }

    public enum FailureKind
    {
        None,
        Disconnected,
        ObjectDisposed,
    }

    private sealed class RecordingJsRuntime(RecordingJsModule module) : IJSRuntime
    {
        public int ImportCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Assert.Equal("import", identifier);
            ImportCount++;
            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    private sealed class RecordingJsModule : IJSObjectReference
    {
        public List<JsCall> Calls { get; } = new();

        public int DisposeCount { get; private set; }

        public FailureKind Failure { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Calls.Add(new JsCall(identifier, args ?? Array.Empty<object?>()));
            if (identifier == "dispose")
            {
                ThrowIfConfigured();
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            ThrowIfConfigured();
            return ValueTask.CompletedTask;
        }

        private void ThrowIfConfigured()
        {
            switch (Failure)
            {
                case FailureKind.Disconnected:
                    throw new JSDisconnectedException("วงจรถูกตัดการเชื่อมต่อ");
                case FailureKind.ObjectDisposed:
                    throw new ObjectDisposedException(nameof(RecordingJsModule));
            }
        }
    }

    private sealed record JsCall(string Identifier, object?[] Arguments);
}
