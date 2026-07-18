using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Characters;

public interface ICharacterMutationSessionFactory
{
    ValueTask<ICharacterMutationSession> OpenAsync(CancellationToken cancellationToken);

    Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
        CharacterMutationEvidence evidence,
        CancellationToken cancellationToken);
}

public interface ICharacterMutationSession : ICatalogMutationSession
{
    Task<bool> LockActiveUniverseAsync(
        Guid universeId,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid universeId,
        string normalizedName,
        CancellationToken cancellationToken);

    void Add(Character character);
}

public sealed record CharacterMutationEvidence
{
    private CharacterMutationEvidence(Character character)
    {
        Id = character.Id;
        UniverseId = character.UniverseId;
        Name = character.Name;
        NormalizedName = character.NormalizedName;
    }

    public Guid Id { get; }

    public Guid UniverseId { get; }

    public string Name { get; }

    public string NormalizedName { get; }

    public static CharacterMutationEvidence Capture(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return new CharacterMutationEvidence(character);
    }
}
