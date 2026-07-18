using Microsoft.EntityFrameworkCore;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.IntegrationTests.Infrastructure;

internal static class CatalogTestSeedRestorer
{
    internal static async Task RestoreAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var category in ProductCategorySeeds.All)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "ProductCategories" ("Id", "Code")
                VALUES ({category.Id}, {category.Code})
                ON CONFLICT ("Id") DO NOTHING
                """,
                cancellationToken);
        }

        foreach (var universe in UniverseSeeds.All)
        {
            var status = universe.Status.ToString();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Universes" (
                    "Id", "DisplayName", "NormalizedDisplayName", "EnglishName",
                    "NormalizedEnglishName", "Slug", "LogoStorageKey", "LogoPublicRelativeUrl",
                    "LogoAltText", "Status", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc",
                    "UpdatedBy", "ArchivedAtUtc", "ArchivedBy")
                VALUES (
                    {universe.Id}, {universe.DisplayName}, {universe.NormalizedDisplayName},
                    {universe.EnglishName}, {universe.NormalizedEnglishName}, {universe.Slug},
                    NULL, NULL, NULL, {status}, {universe.CreatedAtUtc}, {universe.CreatedBy},
                    {universe.CreatedAtUtc}, {universe.CreatedBy}, NULL, NULL)
                ON CONFLICT ("Id") DO NOTHING
                """,
                cancellationToken);
        }
    }
}
