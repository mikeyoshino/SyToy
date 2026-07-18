using System.Reflection;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class BrandTests
{
    private static readonly Guid BrandId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FactoryCreatesActiveNormalizedBrandWithInitialAuditAndNoImage()
    {
        var brand = CreateBrand(displayName: "  บันได  ", englishName: " \uFF22andai  Namco ");

        Assert.Equal(BrandId, brand.Id);
        Assert.Equal("บันได", brand.DisplayName);
        Assert.Equal("บันได", brand.NormalizedDisplayName);
        Assert.Equal("Ｂandai  Namco", brand.EnglishName);
        Assert.Equal("BANDAI NAMCO", brand.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("bandai-namco"), brand.Slug);
        Assert.Equal(CatalogReferenceStatus.Active, brand.Status);
        Assert.Null(brand.Image);
        Assert.False(brand.CanBeUsedByPublishedProduct);
        Assert.Equal(CreatedAtUtc, brand.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, brand.UpdatedAtUtc);
        Assert.Equal("admin-1", brand.CreatedBy);
        Assert.Equal("admin-1", brand.UpdatedBy);
        Assert.Null(brand.ArchivedAtUtc);
        Assert.Null(brand.ArchivedBy);
        Assert.Equal(1, brand.Version);
    }

    [Fact]
    public void AdminFactoryCreatesBrandWithRequiredImageAtVersionOne()
    {
        var image = Media("admin-create");

        var brand = Brand.CreateWithImage(
            BrandId,
            "บันได",
            "Bandai Namco",
            CatalogSlug.Create("bandai-namco"),
            image,
            CreatedAtUtc,
            "admin-1");

        Assert.Equal(image, brand.Image);
        Assert.True(brand.CanBeUsedByPublishedProduct);
        Assert.Equal(1, brand.Version);
    }

    [Fact]
    public void NamesEnforceTrimmedAndNormalizedCentralLimitsWithoutMutation()
    {
        var exactLimit = new string('a', CatalogReferenceLimits.NameLength);
        var brand = CreateBrand(displayName: exactLimit, englishName: exactLimit);

        Assert.Equal(CatalogReferenceLimits.NameLength, brand.DisplayName.Length);
        AssertRule(
            CatalogReferenceRule.NameTooLong,
            () => CreateBrand(displayName: new string('a', CatalogReferenceLimits.NameLength + 1)));
        AssertRule(
            CatalogReferenceRule.NormalizedNameTooLong,
            () => CreateBrand(displayName: new string('\uFB03', 67)));
    }

    [Fact]
    public void AtomicAdminUpdateChangesDetailsAndImageWithOneVersionIncrement()
    {
        var brand = Brand.CreateWithImage(
            BrandId,
            "บันได",
            "Bandai Namco",
            CatalogSlug.Create("bandai-namco"),
            Media("first"),
            CreatedAtUtc,
            "admin-1");
        var replacement = Media("replacement");

        brand.UpdateDetailsWithImage(
            "เมก้าเฮาส์",
            "Mega House",
            CatalogSlug.Create("mega-house"),
            replacement,
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal("เมก้าเฮาส์", brand.DisplayName);
        Assert.Equal("MEGA HOUSE", brand.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("mega-house"), brand.Slug);
        Assert.Equal(replacement, brand.Image);
        Assert.Equal(2, brand.Version);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), brand.UpdatedAtUtc);
    }

    [Fact]
    public void AtomicAdminUpdateNoOpAndStaleVersionLeaveEveryFieldUnchanged()
    {
        var brand = Brand.CreateWithImage(
            BrandId,
            "บันได",
            "Bandai Namco",
            CatalogSlug.Create("bandai-namco"),
            Media("first"),
            CreatedAtUtc,
            "admin-1");
        var before = Snapshot(brand);

        brand.UpdateDetailsWithImage(
            brand.DisplayName,
            brand.EnglishName,
            brand.Slug,
            replacementImage: null,
            expectedVersion: brand.Version,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal(before, Snapshot(brand));
        AssertRule(
            CatalogReferenceRule.ConcurrencyVersionMismatch,
            () => brand.UpdateDetailsWithImage(
                "ชื่อใหม่",
                "New Name",
                CatalogSlug.Create("new-name"),
                Media("new"),
                expectedVersion: brand.Version + 1,
                CreatedAtUtc.AddMinutes(2),
                "admin-3"));
        Assert.Equal(before, Snapshot(brand));
    }

    [Fact]
    public void VersionedArchiveAdvancesOnceAndRejectsStaleWithoutMutation()
    {
        var stale = CreateBrand();
        var staleBefore = Snapshot(stale);
        AssertRule(
            CatalogReferenceRule.ConcurrencyVersionMismatch,
            () => stale.Archive(2, CreatedAtUtc.AddMinutes(1), "admin-2"));
        Assert.Equal(staleBefore, Snapshot(stale));

        var brand = CreateBrand();
        brand.Archive(1, CreatedAtUtc.AddMinutes(1), "admin-2");

        Assert.Equal(2, brand.Version);
        Assert.Equal(CatalogReferenceStatus.Archived, brand.Status);
    }

    [Fact]
    public void FactoryRejectsInvalidIdentityNameSlugAndAuditWithStableRules()
    {
        AssertRule(CatalogReferenceRule.IdentityRequired, () => CreateBrand(id: Guid.Empty));
        AssertRule(CatalogReferenceRule.NameRequired, () => CreateBrand(displayName: " "));
        AssertRule(CatalogReferenceRule.NameRequired, () => CreateBrand(englishName: " "));
        AssertRule(
            CatalogReferenceRule.SlugInvalid,
            () => CreateBrand(slug: (CatalogSlug?)default(CatalogSlug)));
        AssertRule(
            CatalogReferenceRule.AuditInstantMustBeUtc,
            () => CreateBrand(createdAtUtc: CreatedAtUtc.ToOffset(TimeSpan.FromHours(7))));
        AssertRule(CatalogReferenceRule.AuditActorRequired, () => CreateBrand(actor: " "));
    }

    [Fact]
    public void AttachAndReplaceImageAcceptEqualAuditTimestampAndControlsPublishEligibility()
    {
        var brand = CreateBrand();
        var first = Media("front");
        var replacement = Media("replacement");

        brand.AttachOrReplaceImage(first, CreatedAtUtc, "admin-2");

        Assert.Equal(first, brand.Image);
        Assert.True(brand.CanBeUsedByPublishedProduct);
        Assert.Equal(CreatedAtUtc, brand.UpdatedAtUtc);
        Assert.Equal("admin-2", brand.UpdatedBy);

        brand.AttachOrReplaceImage(replacement, CreatedAtUtc.AddMinutes(1), "admin-3");

        Assert.Equal(replacement, brand.Image);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), brand.UpdatedAtUtc);
        Assert.Equal("admin-3", brand.UpdatedBy);
    }

    [Fact]
    public void UpdateDetailsRecomputesNormalizedNamesAndSlugAtomically()
    {
        var brand = CreateBrand();

        brand.UpdateDetails(
            "  เมก้า\u2003เฮาส์ ",
            "Ｍega   House",
            CatalogSlug.Create("mega-house"),
            CreatedAtUtc.AddHours(1),
            "admin-2");

        Assert.Equal("เมก้า\u2003เฮาส์", brand.DisplayName);
        Assert.Equal("เมก้า เฮาส์", brand.NormalizedDisplayName);
        Assert.Equal("Ｍega   House", brand.EnglishName);
        Assert.Equal("MEGA HOUSE", brand.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("mega-house"), brand.Slug);
        Assert.Equal(CreatedAtUtc.AddHours(1), brand.UpdatedAtUtc);
        Assert.Equal("admin-2", brand.UpdatedBy);
    }

    [Theory]
    [InlineData("details", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("details", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("details", "actor", CatalogReferenceRule.AuditActorRequired)]
    [InlineData("image", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("image", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("image", "actor", CatalogReferenceRule.AuditActorRequired)]
    [InlineData("archive", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("archive", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("archive", "actor", CatalogReferenceRule.AuditActorRequired)]
    public void MutationRejectsInvalidAuditWithoutChangingAnyState(
        string mutation,
        string invalidField,
        CatalogReferenceRule expectedRule)
    {
        var brand = CreateBrand();
        var before = Snapshot(brand);
        var changedAtUtc = invalidField switch
        {
            "nonUtc" => CreatedAtUtc.ToOffset(TimeSpan.FromHours(7)),
            "backwards" => CreatedAtUtc.AddTicks(-1),
            _ => CreatedAtUtc,
        };
        var actor = invalidField == "actor" ? " " : "admin-2";

        var exception = Assert.Throws<CatalogReferenceRuleException>(() => mutation switch
        {
            "details" => Invoke(() => brand.UpdateDetails(
                "ชื่อใหม่",
                "New Name",
                CatalogSlug.Create("new-name"),
                changedAtUtc,
                actor)),
            "image" => Invoke(() => brand.AttachOrReplaceImage(Media("new"), changedAtUtc, actor)),
            _ => Invoke(() => brand.Archive(changedAtUtc, actor)),
        });

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(before, Snapshot(brand));
    }

    [Fact]
    public void InvalidDetailsAndMissingMediaLeaveStateUnchanged()
    {
        var brand = CreateBrand();
        var before = Snapshot(brand);

        AssertRule(
            CatalogReferenceRule.NameRequired,
            () => brand.UpdateDetails(
                " ",
                "New Name",
                CatalogSlug.Create("new-name"),
                CreatedAtUtc,
                "admin-2"));
        Assert.Equal(before, Snapshot(brand));

        AssertRule(
            CatalogReferenceRule.MediaRequired,
            () => brand.AttachOrReplaceImage(null!, CreatedAtUtc, "admin-2"));
        Assert.Equal(before, Snapshot(brand));
    }

    [Fact]
    public void ArchiveIsTerminalAndPreservesMediaForHistory()
    {
        var brand = CreateBrand();
        var image = Media("brand");
        brand.AttachOrReplaceImage(image, CreatedAtUtc, "admin-1");

        brand.Archive(CreatedAtUtc.AddHours(1), "admin-2");

        Assert.Equal(CatalogReferenceStatus.Archived, brand.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), brand.ArchivedAtUtc);
        Assert.Equal("admin-2", brand.ArchivedBy);
        Assert.Equal(image, brand.Image);
        Assert.False(brand.CanBeUsedByPublishedProduct);
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => brand.UpdateDetails(
                "ชื่อใหม่",
                "New Name",
                CatalogSlug.Create("new-name"),
                CreatedAtUtc.AddHours(2),
                "admin-3"));
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => brand.AttachOrReplaceImage(Media("new"), CreatedAtUtc.AddHours(2), "admin-3"));
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => brand.Archive(CreatedAtUtc.AddHours(2), "admin-3"));
    }

    [Fact]
    public void BrandHasNoPublicInvalidConstructorOrPublicSetters()
    {
        Assert.Empty(typeof(Brand).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            typeof(Brand).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    private static Brand CreateBrand(
        Guid? id = null,
        string displayName = "บันได",
        string englishName = "Bandai Namco",
        CatalogSlug? slug = null,
        DateTimeOffset? createdAtUtc = null,
        string actor = "admin-1") =>
        Brand.Create(
            id ?? BrandId,
            displayName,
            englishName,
            slug ?? CatalogSlug.Create("bandai-namco"),
            createdAtUtc ?? CreatedAtUtc,
            actor);

    private static CatalogMediaReference Media(string name) =>
        CatalogMediaReference.Create(
            $"brands/{name}.webp",
            $"/media/brands/{name}.webp",
            $"โลโก้ {name}");

    private static object Snapshot(Brand brand) => new
    {
        brand.DisplayName,
        brand.NormalizedDisplayName,
        brand.EnglishName,
        brand.NormalizedEnglishName,
        brand.Slug,
        brand.Image,
        brand.Status,
        brand.UpdatedAtUtc,
        brand.UpdatedBy,
        brand.ArchivedAtUtc,
        brand.ArchivedBy,
        brand.Version,
    };

    private static bool Invoke(Action action)
    {
        action();
        return true;
    }

    private static void AssertRule(CatalogReferenceRule expectedRule, Action action)
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(action);
        Assert.Equal(expectedRule, exception.Rule);
    }
}
