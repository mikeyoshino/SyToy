using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Characters;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class CharacterSearchReader(
    IDbContextFactory<ApplicationDbContext> contextFactory) : ICharacterSearchReader
{
    private const int MaximumLimit = 20;

    public async Task<CharacterSearchReadResult> ReadAsync(
        CharacterSearchReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.NormalizedTerm);
        ArgumentOutOfRangeException.ThrowIfEqual(request.UniverseId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Limit, MaximumLimit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            request.NormalizedTerm.Length,
            CatalogReferenceLimits.NameLength);

        await using var dbContext = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var universeAvailable = await dbContext.Universes
            .AsNoTracking()
            .AnyAsync(
                universe => universe.Id == request.UniverseId
                    && universe.Status == CatalogReferenceStatus.Active,
                cancellationToken);

        if (!universeAvailable)
        {
            return new CharacterSearchReadResult(
                universeAvailable: false,
                items: [],
                hasExactMatch: false);
        }

        var normalizedTerm = request.NormalizedTerm;
        var query = dbContext.Characters
            .AsNoTracking()
            .Where(character => character.UniverseId == request.UniverseId);

        if (normalizedTerm.Length > 0)
        {
            query = query.Where(character => character.NormalizedName.Contains(normalizedTerm));
        }

        var rows = await query
            .OrderByDescending(character =>
                normalizedTerm.Length > 0 && character.NormalizedName == normalizedTerm)
            .ThenBy(character => character.NormalizedName)
            .ThenBy(character => character.Id)
            .Take(request.Limit)
            .Select(character => new CharacterSearchRow(
                character.Id,
                character.UniverseId,
                character.Name,
                normalizedTerm.Length > 0 && character.NormalizedName == normalizedTerm))
            .ToArrayAsync(cancellationToken);
        var items = rows
            .Select(row => new CharacterOption(row.Id, row.UniverseId, row.Name))
            .ToArray();

        return new CharacterSearchReadResult(
            universeAvailable: true,
            items,
            rows.Any(row => row.IsExactMatch));
    }

    private sealed record CharacterSearchRow(
        Guid Id,
        Guid UniverseId,
        string Name,
        bool IsExactMatch);
}
