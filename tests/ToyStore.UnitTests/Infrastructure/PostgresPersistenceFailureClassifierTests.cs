using Microsoft.EntityFrameworkCore;
using Npgsql;
using ToyStore.Application.Common.Persistence;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class PostgresPersistenceFailureClassifierTests
{
    public static TheoryData<string, PersistenceFailureTarget, PersistenceFailureKind>
        ExactNormalizedNameConstraints => new()
        {
            {
                "UX_Brands_NormalizedDisplayName",
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateDisplayName
            },
            {
                "UX_Brands_NormalizedEnglishName",
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateEnglishName
            },
            {
                "UX_Universes_NormalizedDisplayName",
                PersistenceFailureTarget.Universe,
                PersistenceFailureKind.DuplicateDisplayName
            },
            {
                "UX_Universes_NormalizedEnglishName",
                PersistenceFailureTarget.Universe,
                PersistenceFailureKind.DuplicateEnglishName
            },
            {
                "UX_Characters_UniverseId_NormalizedName",
                PersistenceFailureTarget.Character,
                PersistenceFailureKind.DuplicateName
            },
            {
                "UX_Products_NormalizedDisplayName",
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateDisplayName
            },
            {
                "UX_Products_NormalizedEnglishName",
                PersistenceFailureTarget.Product,
                PersistenceFailureKind.DuplicateEnglishName
            },
        };

    [Theory]
    [MemberData(nameof(ExactNormalizedNameConstraints))]
    public void ExactUniqueConstraintMapsToTypedFailure(
        string constraintName,
        PersistenceFailureTarget target,
        PersistenceFailureKind kind)
    {
        var exception = new DbUpdateException(
            "save failed",
            Postgres(PostgresErrorCodes.UniqueViolation, constraintName));

        var failure = PostgresPersistenceFailureClassifier.Instance.Classify(exception);

        Assert.Equal(new PersistenceFailure(target, kind), failure);
    }

    [Theory]
    [InlineData("UX_Brands_Slug")]
    [InlineData("UX_Universes_Slug")]
    [InlineData("ux_brands_normalizeddisplayname")]
    [InlineData("UX_Characters_NormalizedName")]
    [InlineData("ux_characters_universeid_normalizedname")]
    [InlineData("UX_Products_Slug")]
    [InlineData("ux_products_normalizeddisplayname")]
    [InlineData("")]
    public void UnapprovedConstraintNamesAreNotMapped(string constraintName)
    {
        var exception = new DbUpdateException(
            "save failed",
            Postgres(PostgresErrorCodes.UniqueViolation, constraintName));

        Assert.Null(PostgresPersistenceFailureClassifier.Instance.Classify(exception));
    }

    [Fact]
    public void ExactConstraintWithWrongSqlStateIsNotMapped()
    {
        var exception = new DbUpdateException(
            "save failed",
            Postgres(PostgresErrorCodes.CheckViolation, "UX_Brands_NormalizedDisplayName"));

        Assert.Null(PostgresPersistenceFailureClassifier.Instance.Classify(exception));
    }

    [Fact]
    public void DbConcurrencyExceptionMapsWithoutProviderDetails()
    {
        var failure = PostgresPersistenceFailureClassifier.Instance.Classify(
            new DbUpdateConcurrencyException("stale"));

        Assert.Equal(
            new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict),
            failure);
    }

    [Fact]
    public void OtherDatabaseAndSystemFailuresRemainExceptional()
    {
        Assert.Null(PostgresPersistenceFailureClassifier.Instance.Classify(
            new DbUpdateException("network", new InvalidOperationException("offline"))));
        Assert.Null(PostgresPersistenceFailureClassifier.Instance.Classify(
            new InvalidOperationException("unknown")));
    }

    private static PostgresException Postgres(string sqlState, string constraintName) =>
        new(
            "database rejected write",
            "ERROR",
            "ERROR",
            sqlState,
            constraintName: constraintName);
}
