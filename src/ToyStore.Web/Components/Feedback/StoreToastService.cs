namespace ToyStore.Web.Components.Feedback;

public sealed class StoreToastService : IStoreToastService, IAsyncDisposable
{
    private const int MaxToasts = 4;

    private static readonly TimeSpan DefaultAutoDismiss = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan LongAutoDismiss = TimeSpan.FromSeconds(6);

    private readonly List<StoreToastItem> toasts = [];
    private readonly Dictionary<Guid, CancellationTokenSource> dismissalTimers = [];
    private readonly object gate = new();

    public event Action? ToastsUpdated;

    public IReadOnlyList<StoreToastItem> Toasts
    {
        get
        {
            lock (gate)
            {
                return [.. toasts];
            }
        }
    }

    public Guid Show(
        StoreAlertTone tone,
        string message,
        string? title = null,
        bool dismissible = true,
        bool autoDismiss = true,
        TimeSpan? autoDismissAfter = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Guid.Empty;
        }

        var item = new StoreToastItem(
            Guid.NewGuid(),
            tone,
            message.Trim(),
            title?.Trim(),
            dismissible,
            autoDismiss,
            autoDismissAfter ?? DefaultAutoDismiss);
        AddToast(item);
        return item.Id;
    }

    public Guid ShowSuccess(string message, string? title = null) =>
        Show(StoreAlertTone.Success, message, title, autoDismiss: true, autoDismissAfter: DefaultAutoDismiss);

    public Guid ShowError(string message, string? title = null) =>
        Show(StoreAlertTone.Error, message, title, autoDismiss: true, autoDismissAfter: LongAutoDismiss);

    public Guid ShowWarning(string message, string? title = null) =>
        Show(StoreAlertTone.Warning, message, title, autoDismiss: true, autoDismissAfter: LongAutoDismiss);

    public Guid ShowInfo(string message, string? title = null) =>
        Show(StoreAlertTone.Info, message, title, autoDismiss: true, autoDismissAfter: DefaultAutoDismiss);

    public void Dismiss(Guid id)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        if (!TryRemove(id))
        {
            return;
        }

        NotifyUpdated();
    }

    public void DismissAll()
    {
        lock (gate)
        {
            if (toasts.Count == 0)
            {
                return;
            }

            foreach (var timer in dismissalTimers.Values)
            {
                timer.Cancel();
            }

            dismissalTimers.Clear();
            toasts.Clear();
        }

        NotifyUpdated();
    }

    public async ValueTask DisposeAsync()
    {
        List<CancellationTokenSource> timers;
        lock (gate)
        {
            timers = [.. dismissalTimers.Values];
            dismissalTimers.Clear();
            toasts.Clear();
        }

        foreach (var timer in timers)
        {
            timer.Cancel();
            timer.Dispose();
        }
    }

    private void AddToast(StoreToastItem item)
    {
        CancellationTokenSource? timer = null;
        lock (gate)
        {
            toasts.Insert(0, item);

            if (toasts.Count > MaxToasts)
            {
                var removed = toasts[^1];
                toasts.RemoveAt(toasts.Count - 1);
                if (dismissalTimers.TryGetValue(removed.Id, out var oldTimer))
                {
                    oldTimer.Cancel();
                    oldTimer.Dispose();
                    dismissalTimers.Remove(removed.Id);
                }
            }

            if (item.AutoDismiss)
            {
                timer = new CancellationTokenSource();
                dismissalTimers[item.Id] = timer;
            }
        }

        if (timer is not null)
        {
            _ = MonitorToastLifetimeAsync(item.Id, item.AutoDismissAfter, timer);
        }

        NotifyUpdated();
    }

    private bool TryRemove(Guid id)
    {
        bool removed;
        lock (gate)
        {
            var index = toasts.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                removed = false;
            }
            else
            {
                removed = true;
                toasts.RemoveAt(index);
            }

            if (dismissalTimers.TryGetValue(id, out var timer))
            {
                timer.Cancel();
                timer.Dispose();
                dismissalTimers.Remove(id);
            }
        }

        return removed;
    }

    private async Task MonitorToastLifetimeAsync(Guid id, TimeSpan autoDismissAfter, CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(autoDismissAfter, timer.Token).ConfigureAwait(false);
            Dismiss(id);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            lock (gate)
            {
                dismissalTimers.Remove(id);
            }
        }
    }

    private void NotifyUpdated()
    {
        ToastsUpdated?.Invoke();
    }
}
