using Microsoft.EntityFrameworkCore;
using Npgsql;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Infrastructure.Persistence;

public sealed class PostgresPersistenceFailureClassifier : IPersistenceFailureClassifier
{
    public static PostgresPersistenceFailureClassifier Instance { get; } = new();

    public PersistenceFailure? Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DbUpdateConcurrencyException)
        {
            return new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict);
        }

        if (exception is not DbUpdateException
            {
                InnerException: PostgresException postgresException,
            }
            || postgresException.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        return postgresException.ConstraintName switch
        {
            "UX_Brands_NormalizedDisplayName" => new PersistenceFailure(
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateDisplayName),
            "UX_Brands_NormalizedEnglishName" => new PersistenceFailure(
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateEnglishName),
            "UX_Universes_NormalizedDisplayName" => new PersistenceFailure(
                PersistenceFailureTarget.Universe,
                PersistenceFailureKind.DuplicateDisplayName),
            "UX_Universes_NormalizedEnglishName" => new PersistenceFailure(
                PersistenceFailureTarget.Universe,
                PersistenceFailureKind.DuplicateEnglishName),
            "UX_Characters_UniverseId_NormalizedName" => new PersistenceFailure(
                PersistenceFailureTarget.Character,
                PersistenceFailureKind.DuplicateName),
            "UX_Products_NormalizedDisplayName" => new PersistenceFailure(
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateDisplayName),
            "UX_Products_NormalizedEnglishName" => new PersistenceFailure(
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateEnglishName),
            "PK_StockMovements" => new PersistenceFailure(
                PersistenceFailureTarget.StockMovement,
                PersistenceFailureKind.DuplicateOperation),
            "PK_CartOperations" => new PersistenceFailure(
                PersistenceFailureTarget.CartOperation,
                PersistenceFailureKind.DuplicateOperation),
            "UX_Carts_CustomerId" => new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict),
            _ => null,
        };
    }
}
