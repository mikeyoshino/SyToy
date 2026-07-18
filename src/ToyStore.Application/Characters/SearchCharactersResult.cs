namespace ToyStore.Application.Characters;

public sealed record SearchCharactersResult
{
    public SearchCharactersResult(
        IReadOnlyList<CharacterOption> items,
        bool hasExactMatch)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items.ToArray();
        HasExactMatch = hasExactMatch;
    }

    public IReadOnlyList<CharacterOption> Items { get; }

    public bool HasExactMatch { get; }
}
