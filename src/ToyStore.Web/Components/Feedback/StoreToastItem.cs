namespace ToyStore.Web.Components.Feedback;

public sealed record StoreToastItem(
    Guid Id,
    StoreAlertTone Tone,
    string Message,
    string? Title,
    bool Dismissible,
    bool AutoDismiss,
    TimeSpan AutoDismissAfter);
