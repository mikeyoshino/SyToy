using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Characters.SearchCharacters;

public sealed class SearchCharactersHandler(ICharacterSearchReader reader)
    : IRequestHandler<SearchCharactersQuery, Result<SearchCharactersResult>>
{
    public async Task<Result<SearchCharactersResult>> Handle(
        SearchCharactersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedTerm = string.IsNullOrWhiteSpace(request.Term)
            ? string.Empty
            : CatalogNameNormalizer.Normalize(request.Term);
        var readResult = await reader.ReadAsync(
            new CharacterSearchReadRequest(
                request.UniverseId,
                normalizedTerm,
                request.Limit),
            cancellationToken);

        return readResult.UniverseAvailable
            ? Result<SearchCharactersResult>.Success(new SearchCharactersResult(
                readResult.Items,
                readResult.HasExactMatch))
            : Result<SearchCharactersResult>.Failure(CharacterErrors.UniverseUnavailable);
    }
}
