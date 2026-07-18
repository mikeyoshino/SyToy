using Microsoft.AspNetCore.Components.Forms;

namespace ToyStore.Web.Components.Forms;

internal sealed class SingleImagePreviewState
{
    private IBrowserFile? observedValue;
    private bool hasObservedValue;

    public string? PreviewUrl { get; private set; }

    public bool ObserveValue(IBrowserFile? value)
    {
        var resetRequested = hasObservedValue
            && observedValue is not null
            && value is null;

        hasObservedValue = true;
        observedValue = value;
        if (resetRequested)
        {
            PreviewUrl = null;
        }

        return resetRequested;
    }

    public void SetPreview(string? previewUrl)
    {
        PreviewUrl = previewUrl;
    }

    public void Clear()
    {
        hasObservedValue = true;
        observedValue = null;
        PreviewUrl = null;
    }
}
