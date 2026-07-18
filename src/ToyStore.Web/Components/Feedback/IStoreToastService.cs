namespace ToyStore.Web.Components.Feedback;

public interface IStoreToastService
{
    IReadOnlyList<StoreToastItem> Toasts { get; }

    event Action? ToastsUpdated;

    Guid Show(
        StoreAlertTone tone,
        string message,
        string? title = null,
        bool dismissible = true,
        bool autoDismiss = true,
        TimeSpan? autoDismissAfter = null);

    Guid ShowSuccess(string message, string? title = null);

    Guid ShowError(string message, string? title = null);

    Guid ShowWarning(string message, string? title = null);

    Guid ShowInfo(string message, string? title = null);

    void Dismiss(Guid id);

    void DismissAll();
}
