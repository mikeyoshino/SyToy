namespace ToyStore.Web.Components.Forms;

internal sealed class AutocompleteOptionContract<TOption, TKey>
{
    public AutocompleteOptionContract(
        Func<TOption, TKey> key,
        Func<TOption, string> label,
        Func<TOption, string> id,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(id);

        Key = key;
        Label = label;
        Id = id;
        KeyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
    }

    public Func<TOption, TKey> Key { get; }

    public Func<TOption, string> Label { get; }

    public Func<TOption, string> Id { get; }

    public IEqualityComparer<TKey> KeyComparer { get; }
}
