namespace ToyStore.Application.Characters;

public interface ICharacterSearchReader
{
    Task<CharacterSearchReadResult> ReadAsync(
        CharacterSearchReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record CharacterSearchReadRequest(
    Guid UniverseId,
    string NormalizedTerm,
    int Limit);

public sealed record CharacterSearchReadResult
{
    public CharacterSearchReadResult(
        bool universeAvailable,
        IReadOnlyList<CharacterOption> items,
        bool hasExactMatch)
    {
        ArgumentNullException.ThrowIfNull(items);
        UniverseAvailable = universeAvailable;
        Items = items.ToArray();
        HasExactMatch = hasExactMatch;
    }

    public bool UniverseAvailable { get; }

    public IReadOnlyList<CharacterOption> Items { get; }

    public bool HasExactMatch { get; }
}
