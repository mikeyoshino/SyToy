using System.Reflection;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class CharacterTests
{
    private static readonly Guid ProductId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid CharacterId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherCharacterId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid UniverseId = Guid.Parse("50000000-0000-0000-0000-000000000004");
    private static readonly Guid OtherUniverseId = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FactoryCreatesNormalizedScopedIdentityAndRejectsInvalidValues()
    {
        var character = Character.Create(CharacterId, UniverseId, "  Ｉron\u2003  Man ");

        Assert.Equal(CharacterId, character.Id);
        Assert.Equal(UniverseId, character.UniverseId);
        Assert.Equal("Ｉron\u2003  Man", character.Name);
        Assert.Equal("IRON MAN", character.NormalizedName);
        Assert.Equal(CharacterIdentity.Create(UniverseId, "iron man"), character.Identity);

        AssertRule(CatalogReferenceRule.IdentityRequired, () => Character.Create(Guid.Empty, UniverseId, "Iron Man"));
        AssertRule(CatalogReferenceRule.UniverseRequired, () => Character.Create(CharacterId, Guid.Empty, "Iron Man"));
        AssertRule(CatalogReferenceRule.NameRequired, () => Character.Create(CharacterId, UniverseId, " "));
    }

    [Fact]
    public void CharacterIdentityUsesNormalizedNameWithinUniverse()
    {
        var first = CharacterIdentity.Create(UniverseId, "Ｃafe\u0301\u2003Hero");
        var equivalent = CharacterIdentity.Create(UniverseId, "Caf\u00E9 hero");
        var differentUniverse = CharacterIdentity.Create(OtherUniverseId, "Caf\u00E9 hero");

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(first, differentUniverse);
    }

    [Fact]
    public void NamesEnforceTrimmedAndNormalizedCentralLimitsWithoutPartialCreation()
    {
        var exactPersistedLimit = new string('a', CatalogReferenceLimits.NameLength);
        var exactNormalizedLimit = string.Concat(new string('\uFB03', 66), "aa");

        var persistedBoundary = Character.Create(CharacterId, UniverseId, $"  {exactPersistedLimit}  ");
        var normalizedBoundary = Character.Create(OtherCharacterId, UniverseId, exactNormalizedLimit);

        Assert.Equal(CatalogReferenceLimits.NameLength, persistedBoundary.Name.Length);
        Assert.Equal(CatalogReferenceLimits.NameLength, normalizedBoundary.NormalizedName.Length);
        AssertRule(
            CatalogReferenceRule.NameTooLong,
            () => Character.Create(Guid.NewGuid(), UniverseId, new string('a', CatalogReferenceLimits.NameLength + 1)));
        AssertRule(
            CatalogReferenceRule.NormalizedNameTooLong,
            () => Character.Create(Guid.NewGuid(), UniverseId, new string('\uFB03', 67)));
    }

    [Fact]
    public void CharacterIsCreationOnlyAndHasNoInvalidPublicConstructorOrMutation()
    {
        Assert.Empty(typeof(Character).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(typeof(Character).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.DoesNotContain(
            typeof(Character).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => !method.IsSpecialName);
    }

    [Fact]
    public void ProductCharacterHasStructuralEqualityAndNoInvalidPublicConstructor()
    {
        var first = ProductCharacter.Create(ProductId, CharacterId);
        var second = ProductCharacter.Create(ProductId, CharacterId);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Empty(typeof(ProductCharacter).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(typeof(ProductCharacter).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void ProductFactoryOwnsDistinctReadOnlyCharacterLinksAtVersionOne()
    {
        var product = CreateProduct([OtherCharacterId, CharacterId]);

        Assert.Equal([CharacterId, OtherCharacterId], product.Characters.Select(link => link.CharacterId));
        Assert.All(product.Characters, link => Assert.Equal(ProductId, link.ProductId));
        Assert.Equal(1, product.Version);
        var mutableView = Assert.IsAssignableFrom<IList<ProductCharacter>>(product.Characters);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(ProductCharacter.Create(ProductId, Guid.NewGuid())));
    }

    [Fact]
    public void AtomicDraftUpdateReplacesCharacterSetWithOneVersionIncrement()
    {
        var product = CreateProduct([CharacterId]);

        UpdateCharacters(product, [OtherCharacterId], expectedVersion: 1, CreatedAtUtc.AddMinutes(1), "admin-2");

        Assert.Equal([OtherCharacterId], product.Characters.Select(link => link.CharacterId));
        Assert.Equal(2, product.Version);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), product.UpdatedAtUtc);
        Assert.Equal("admin-2", product.UpdatedBy);
    }

    [Theory]
    [InlineData("empty", ProductRule.ProductCharacterIdentityRequired)]
    [InlineData("duplicate", ProductRule.ProductCharacterDuplicate)]
    public void InvalidCompleteCharacterSetIsRejectedAtomically(
        string invalidCase,
        ProductRule expectedRule)
    {
        var product = CreateProduct([CharacterId]);
        var before = Snapshot(product);
        var replacement = invalidCase == "empty"
            ? new[] { Guid.Empty }
            : new[] { OtherCharacterId, OtherCharacterId };

        var exception = Assert.Throws<ProductRuleException>(() => UpdateCharacters(
            product,
            replacement,
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(before, Snapshot(product));
    }

    private static Product CreateProduct(IReadOnlyCollection<Guid>? characters = null) =>
        Product.CreateInStock(
            ProductId,
            "กันดั้ม",
            "Gundam",
            "โมเดลสะสม",
            "gundam",
            CatalogSeedIds.GundamCategory,
            Guid.Parse("50000000-0000-0000-0000-000000000006"),
            UniverseId,
            InStockOffer.Create(Money.Create(1490)),
            [],
            characters ?? [],
            CreatedAtUtc,
            "admin-1");

    private static void UpdateCharacters(
        Product product,
        IReadOnlyCollection<Guid> characterIds,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) => product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            product.InStockOffer!,
            product.Images.Select(image => new ProductImageDefinition(
                image.Id,
                image.StorageKey,
                image.PublicRelativeUrl,
                image.AltText)).ToArray(),
            characterIds,
            expectedVersion,
            changedAtUtc,
            actor);

    private static object Snapshot(Product product) => new
    {
        Links = string.Join(",", product.Characters.Select(link => link.CharacterId)),
        product.UpdatedAtUtc,
        product.UpdatedBy,
        product.Status,
        product.Version,
    };

    private static void AssertRule(CatalogReferenceRule expectedRule, Action action)
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(action);
        Assert.Equal(expectedRule, exception.Rule);
    }
}
