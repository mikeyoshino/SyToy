namespace ToyStore.Web.Components.Forms;

public sealed class AutocompleteMultiSelectCopy
{
    public AutocompleteMultiSelectCopy(
        string label,
        string placeholder,
        string loading,
        string empty,
        Func<int, string> results,
        Func<string, string> createLabel,
        Func<string, string> removeLabel,
        Func<string, string> selected,
        Func<string, string> removed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);
        ArgumentException.ThrowIfNullOrWhiteSpace(loading);
        ArgumentException.ThrowIfNullOrWhiteSpace(empty);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(createLabel);
        ArgumentNullException.ThrowIfNull(removeLabel);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(removed);

        Label = label;
        Placeholder = placeholder;
        Loading = loading;
        Empty = empty;
        Results = results;
        CreateLabel = createLabel;
        RemoveLabel = removeLabel;
        Selected = selected;
        Removed = removed;
    }

    public string Label { get; }

    public string Placeholder { get; }

    public string Loading { get; }

    public string Empty { get; }

    public Func<int, string> Results { get; }

    public Func<string, string> CreateLabel { get; }

    public Func<string, string> RemoveLabel { get; }

    public Func<string, string> Selected { get; }

    public Func<string, string> Removed { get; }
}
