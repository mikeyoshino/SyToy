using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Characters.CreateCharacter;

public sealed class CreateCharacterHandler(
    ICharacterMutationSessionFactory sessionFactory,
    CatalogCommitOutcomeResolver commitOutcomeResolver)
    : IRequestHandler<CreateCharacterCommand, Result<CharacterOption>>
{
    public async Task<Result<CharacterOption>> Handle(
        CreateCharacterCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before creating a Character.");

        CharacterMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async operationCancellationToken =>
            {
                if (!await session.LockActiveUniverseAsync(
                        request.UniverseId,
                        operationCancellationToken))
                {
                    return Result<CharacterOption>.Failure(
                        CharacterErrors.UniverseUnavailable);
                }

                var character = Character.Create(
                    Guid.NewGuid(),
                    request.UniverseId,
                    request.Name);
                if (await session.NameExistsAsync(
                        character.UniverseId,
                        character.NormalizedName,
                        operationCancellationToken))
                {
                    return Result<CharacterOption>.Failure(CharacterErrors.DuplicateName);
                }

                session.Add(character);
                intendedEvidence = CharacterMutationEvidence.Capture(character);
                return Result<CharacterOption>.Success(ToOption(character));
            },
            cancellationToken);

        return await commitOutcomeResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "CreateCharacter commit verification requires intended evidence."),
                verificationCancellationToken),
            evidence => new CharacterOption(evidence.Id, evidence.UniverseId, evidence.Name),
            "Character",
            cancellationToken);
    }

    private static CharacterOption ToOption(Character character) =>
        new(character.Id, character.UniverseId, character.Name);
}
