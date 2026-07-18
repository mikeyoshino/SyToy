using Npgsql;
using ToyStore.Domain.Catalog;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogConstraintTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ReferenceUniqueSlugAndForeignKeyConstraintsRejectInvalidDirectWrites()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, BrandSql(Guid.NewGuid(), "แบรนด์หนึ่ง", "BRAND ONE", "Brand One", "BRAND ONE", "brand-one"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, BrandSql(Guid.NewGuid(), "แบรนด์หนึ่ง", "BRAND ONE", "Other", "OTHER", "other")));
        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, BrandSql(Guid.NewGuid(), "Bad Slug", "BAD SLUG", "Bad Slug", "BAD SLUG", "Bad_Slug")));

        var characterName = "IRON MAN " + Guid.NewGuid().ToString("N");
        await ExecuteAsync(connection, CharacterSql(Guid.NewGuid(), MarvelId, characterName, characterName));
        await AssertSqlStateAsync(
            PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, CharacterSql(Guid.NewGuid(), MarvelId, "Duplicate", characterName)));
        await ExecuteAsync(connection, CharacterSql(Guid.NewGuid(), DcId, "Same in DC", characterName));
        await AssertSqlStateAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(connection, CharacterSql(Guid.NewGuid(), Guid.NewGuid(), "No universe", "NO UNIVERSE")));
    }

    [Theory]
    [InlineData("missing-offer", "InStock", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL")]
    [InlineData("mixed-offer", "InStock", "100", "200", "50", "'2026-12-31T16:59:59Z'", "12", "2026", "10", "2", "7")]
    [InlineData("bad-price", "InStock", "-1", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL")]
    [InlineData("bad-deposit", "PreOrder", "NULL", "100", "100", "'2026-12-31T16:59:59Z'", "12", "2026", "10", "2", "7")]
    [InlineData("bad-capacity", "PreOrder", "NULL", "100", "20", "'2026-12-31T16:59:59Z'", "12", "2026", "0", "1", "7")]
    [InlineData("bad-limit", "PreOrder", "NULL", "100", "20", "'2026-12-31T16:59:59Z'", "12", "2026", "2", "3", "7")]
    [InlineData("bad-month", "PreOrder", "NULL", "100", "20", "'2026-12-31T16:59:59Z'", "13", "2026", "10", "2", "7")]
    [InlineData("bad-days", "PreOrder", "NULL", "100", "20", "'2026-12-31T16:59:59Z'", "12", "2026", "10", "2", "0")]
    public async Task ProductOfferChecksRejectImpossibleDirectRows(
        string suffix,
        string saleType,
        string inStockPrice,
        string fullPrice,
        string deposit,
        string closeAt,
        string etaMonth,
        string etaYear,
        string capacity,
        string maxPerCustomer,
        string balanceDays)
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, $"Brand {suffix}", $"BRAND {suffix}", $"English {suffix}", $"ENGLISH {suffix}", $"brand-{suffix}"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, ProductSql(
                Guid.NewGuid(), suffix, brandId, saleType, inStockPrice, fullPrice, deposit,
                closeAt, etaMonth, etaYear, capacity, maxPerCustomer, balanceDays)));
    }

    [Fact]
    public async Task ImageOrderPrimaryStorageAndRelationConstraintsRejectInvalidDirectWrites()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, "Image Brand", "IMAGE BRAND", "Image Brand", "IMAGE BRAND", "image-brand"));
        await ExecuteAsync(connection, ProductSql(
            productId, "valid-image-product", brandId, "InStock", "100", "NULL", "NULL",
            "NULL", "NULL", "NULL", "NULL", "NULL", "NULL"));
        await ExecuteAsync(connection, ProductSql(
            otherProductId, "valid-image-product-2", brandId, "InStock", "100", "NULL", "NULL",
            "NULL", "NULL", "NULL", "NULL", "NULL", "NULL"));
        await ExecuteAsync(connection, ImageSql(Guid.NewGuid(), productId, "main", 0, true));
        await ExecuteAsync(connection, ImageSql(Guid.NewGuid(), productId, "second", 1, false));

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, ImageSql(Guid.NewGuid(), productId, "negative", -1, false)));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, ImageSql(Guid.NewGuid(), productId, "wrong-primary", 1, true)));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, ImageSql(Guid.NewGuid(), productId, "same-order", 1, false)));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, ImageSql(Guid.NewGuid(), otherProductId, "main", 0, true)));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(connection, ImageSql(Guid.NewGuid(), Guid.NewGuid(), "missing-product", 0, true)));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(connection, ProductSql(
                Guid.NewGuid(), "missing-relation", Guid.NewGuid(), "InStock", "100", "NULL", "NULL",
                "NULL", "NULL", "NULL", "NULL", "NULL", "NULL")));
    }

    [Theory]
    [InlineData("close-before-created", "'2026-07-16T16:59:59Z'", "7", "2026")]
    [InlineData("not-bangkok-close", "'2026-12-31T16:59:58Z'", "12", "2026")]
    [InlineData("eta-before-close", "'2026-12-31T16:59:59Z'", "11", "2026")]
    public async Task PreOrderDateChecksRejectInvalidBusinessBoundaries(
        string suffix,
        string closeAt,
        string etaMonth,
        string etaYear)
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, suffix, suffix.ToUpperInvariant(), suffix, suffix.ToUpperInvariant(), suffix));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, ProductSql(
                Guid.NewGuid(), suffix, brandId, "PreOrder", "NULL", "100", "20", closeAt,
                etaMonth, etaYear, "10", "2", "7")));
    }

    [Fact]
    public async Task PartialBrandImageAndUniverseLogoRowsAreRejected()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, "Media Brand", "MEDIA BRAND", "Media Brand", "MEDIA BRAND", "media-brand"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, $"UPDATE \"Brands\" SET \"ImageStorageKey\" = 'partial' WHERE \"Id\" = '{brandId}';"));
        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, $"UPDATE \"Universes\" SET \"LogoStorageKey\" = 'partial' WHERE \"Id\" = '{UnknownId}';"));
    }

    [Fact]
    public async Task BrandImageAndUniverseLogoRejectWhitespaceOnlyMediaFields()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, "Whitespace Brand", "WHITESPACE BRAND", "Whitespace Brand", "WHITESPACE BRAND", "whitespace-brand"));

        foreach (var column in new[] { "ImageStorageKey", "ImagePublicRelativeUrl", "ImageAltText" })
        {
            await AssertSqlStateAsync(
                PostgresErrorCodes.CheckViolation,
                () => ExecuteAsync(connection, $"""
                    UPDATE "Brands" SET
                        "ImageStorageKey" = CASE WHEN '{column}' = 'ImageStorageKey' THEN E' \t ' ELSE 'key' END,
                        "ImagePublicRelativeUrl" = CASE WHEN '{column}' = 'ImagePublicRelativeUrl' THEN E' \t ' ELSE '/media/key' END,
                        "ImageAltText" = CASE WHEN '{column}' = 'ImageAltText' THEN E' \t ' ELSE 'alt' END
                    WHERE "Id" = '{brandId}';
                    """));
        }

        foreach (var column in new[] { "LogoStorageKey", "LogoPublicRelativeUrl", "LogoAltText" })
        {
            await AssertSqlStateAsync(
                PostgresErrorCodes.CheckViolation,
                () => ExecuteAsync(connection, $"""
                    UPDATE "Universes" SET
                        "LogoStorageKey" = CASE WHEN '{column}' = 'LogoStorageKey' THEN E' \t ' ELSE 'key' END,
                        "LogoPublicRelativeUrl" = CASE WHEN '{column}' = 'LogoPublicRelativeUrl' THEN E' \t ' ELSE '/media/key' END,
                        "LogoAltText" = CASE WHEN '{column}' = 'LogoAltText' THEN E' \t ' ELSE 'alt' END
                    WHERE "Id" = '{UnknownId}';
                    """));
        }
    }

    [Fact]
    public async Task EveryCatalogUniqueAndSlugConstraintRejectsItsOwnDirectViolation()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, "Unique Brand", "UNIQUE BRAND", "Unique Brand", "UNIQUE BRAND", "unique-brand"));
        await ExecuteAsync(connection, ProductSql(
            Guid.NewGuid(), "unique-product", brandId, "InStock", "100", "NULL", "NULL",
            "NULL", "NULL", "NULL", "NULL", "NULL", "NULL"));

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "display-duplicate", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", normalizedDisplayName: "PRODUCT UNIQUE-PRODUCT")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "english-duplicate", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", normalizedEnglishName: "ENGLISH UNIQUE-PRODUCT")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "slug-duplicate", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", slug: "unique-product")));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "newline-slug", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", slug: "newline-slug\n")));

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            BrandSql(Guid.NewGuid(), "Other Brand English", "OTHER BRAND ENGLISH", "Other", "UNIQUE BRAND", "other-brand-english")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            BrandSql(Guid.NewGuid(), "Other Brand Slug", "OTHER BRAND SLUG", "Other Slug", "OTHER SLUG", "unique-brand")));

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            UniverseSql(Guid.NewGuid(), "Marvel duplicate", "MARVEL", "Marvel Other", "MARVEL OTHER", "marvel-other")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            UniverseSql(Guid.NewGuid(), "Marvel English", "MARVEL ENGLISH", "Marvel", "MARVEL", "marvel-english")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            UniverseSql(Guid.NewGuid(), "Marvel Slug", "MARVEL SLUG", "Marvel Slug", "MARVEL SLUG", "marvel")));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => ExecuteAsync(connection,
            UniverseSql(Guid.NewGuid(), "Bad Universe", "BAD UNIVERSE", "Bad Universe", "BAD UNIVERSE", "Bad_Universe")));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            $"INSERT INTO \"ProductCategories\" (\"Id\", \"Code\") VALUES ('{Guid.NewGuid()}', 'Gundam');"));
    }

    [Fact]
    public async Task EveryCatalogForeignKeyRejectsItsOwnMissingPrincipal()
    {
        await using var factory = await StartAndResetAsync();
        await using var connection = await OpenAsync();
        var brandId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        await ExecuteAsync(connection, BrandSql(brandId, "FK Brand", "FK BRAND", "FK Brand", "FK BRAND", "fk-brand"));
        await ExecuteAsync(connection, ProductSql(
            productId, "fk-product", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL"));
        await ExecuteAsync(connection, CharacterSql(characterId, UnknownId, "FK Character", "FK CHARACTER"));
        await ExecuteAsync(connection, ProductCharacterSql(productId, characterId));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => ExecuteAsync(connection,
            ProductCharacterSql(productId, characterId)));

        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "bad-category-fk", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", categoryId: Guid.NewGuid())));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "bad-brand-fk", Guid.NewGuid(), "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL")));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => ExecuteAsync(connection,
            ProductSql(Guid.NewGuid(), "bad-universe-fk", brandId, "InStock", "100", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", "NULL", universeId: Guid.NewGuid())));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => ExecuteAsync(connection,
            ProductCharacterSql(Guid.NewGuid(), characterId)));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, () => ExecuteAsync(connection,
            ProductCharacterSql(productId, Guid.NewGuid())));
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task AssertSqlStateAsync(string expected, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(expected, exception.SqlState);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string BrandSql(
        Guid id,
        string displayName,
        string normalizedDisplayName,
        string englishName,
        string normalizedEnglishName,
        string slug) => FormattableString.Invariant($$"""
            INSERT INTO "Brands" (
                "Id", "DisplayName", "NormalizedDisplayName", "EnglishName", "NormalizedEnglishName",
                "Slug", "Status", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy") VALUES (
                '{{id}}', '{{displayName}}', '{{normalizedDisplayName}}', '{{englishName}}', '{{normalizedEnglishName}}',
                '{{slug}}', 'Active', '2026-07-17T00:00:00Z', 'test', '2026-07-17T00:00:00Z', 'test');
            """);

    private static string CharacterSql(Guid id, Guid universeId, string name, string normalizedName) =>
        FormattableString.Invariant($$"""
            INSERT INTO "Characters" ("Id", "UniverseId", "Name", "NormalizedName")
            VALUES ('{{id}}', '{{universeId}}', '{{name}}', '{{normalizedName}}');
            """);

    private static string UniverseSql(
        Guid id,
        string displayName,
        string normalizedDisplayName,
        string englishName,
        string normalizedEnglishName,
        string slug) => FormattableString.Invariant($$"""
            INSERT INTO "Universes" (
                "Id", "DisplayName", "NormalizedDisplayName", "EnglishName", "NormalizedEnglishName",
                "Slug", "Status", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy") VALUES (
                '{{id}}', '{{displayName}}', '{{normalizedDisplayName}}', '{{englishName}}', '{{normalizedEnglishName}}',
                '{{slug}}', 'Active', '2026-07-17T00:00:00Z', 'test', '2026-07-17T00:00:00Z', 'test');
            """);

    private static string ProductCharacterSql(Guid productId, Guid characterId) =>
        FormattableString.Invariant($$"""
            INSERT INTO "ProductCharacters" ("ProductId", "CharacterId")
            VALUES ('{{productId}}', '{{characterId}}');
            """);

    private static string ProductSql(
        Guid id,
        string suffix,
        Guid brandId,
        string saleType,
        string inStockPrice,
        string fullPrice,
        string deposit,
        string closeAt,
        string etaMonth,
        string etaYear,
        string capacity,
        string maxPerCustomer,
        string balanceDays,
        string? normalizedDisplayName = null,
        string? normalizedEnglishName = null,
        string? slug = null,
        Guid? categoryId = null,
        Guid? universeId = null) => FormattableString.Invariant($$"""
            INSERT INTO "Products" (
                "Id", "DisplayName", "NormalizedDisplayName", "EnglishName", "NormalizedEnglishName",
                "Description", "Slug", "ProductCategoryId", "BrandId", "UniverseId", "SaleType", "Status",
                "InStockPrice", "PreOrderFullPrice", "PreOrderDepositAmount", "PreOrderCloseAtUtc",
                "PreOrderEstimatedArrivalMonth", "PreOrderEstimatedArrivalYear", "PreOrderTotalCapacity",
                "PreOrderMaxPerCustomer", "PreOrderBalancePaymentDays", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy")
            VALUES (
                '{{id}}', 'Product {{suffix}}', '{{normalizedDisplayName ?? $"PRODUCT {suffix}".ToUpperInvariant()}}', 'English {{suffix}}', '{{normalizedEnglishName ?? $"ENGLISH {suffix}".ToUpperInvariant()}}',
                'Description', '{{slug ?? suffix}}', '{{categoryId ?? GundamCategoryId}}', '{{brandId}}', '{{universeId ?? UnknownId}}', '{{saleType}}', 'Draft',
                {{inStockPrice}}, {{fullPrice}}, {{deposit}}, {{closeAt}}, {{etaMonth}}, {{etaYear}}, {{capacity}},
                {{maxPerCustomer}}, {{balanceDays}}, '2026-07-17T00:00:00Z', 'test', '2026-07-17T00:00:00Z', 'test');
            """);

    private static string ImageSql(Guid id, Guid productId, string storageKey, int sortOrder, bool primary) =>
        FormattableString.Invariant($$"""
            INSERT INTO "ProductImages" ("Id", "StorageKey", "PublicRelativeUrl", "AltText", "SortOrder", "IsPrimary", "ProductId")
            VALUES ('{{id}}', '{{storageKey}}', '/media/{{storageKey}}', 'alt', {{sortOrder}}, {{primary}}, '{{productId}}');
            """);

    private static readonly Guid GundamCategoryId = CatalogSeedIds.GundamCategory;
    private static readonly Guid MarvelId = CatalogSeedIds.MarvelUniverse;
    private static readonly Guid DcId = CatalogSeedIds.DcUniverse;
    private static readonly Guid UnknownId = CatalogSeedIds.UnknownUniverse;
}
