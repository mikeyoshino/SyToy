using System.Reflection;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class UniverseTests
{
    private static readonly Guid UniverseId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FactoryCreatesActiveNormalizedUniverseThatNeedsLogoForPublication()
    {
        var universe = CreateUniverse(displayName: "  มาร์เวล  ", englishName: " Ｍarvel ");

        Assert.Equal(UniverseId, universe.Id);
        Assert.Equal("มาร์เวล", universe.DisplayName);
        Assert.Equal("มาร์เวล", universe.NormalizedDisplayName);
        Assert.Equal("Ｍarvel", universe.EnglishName);
        Assert.Equal("MARVEL", universe.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("marvel"), universe.Slug);
        Assert.Equal(CatalogReferenceStatus.Active, universe.Status);
        Assert.Null(universe.Logo);
        Assert.False(universe.CanBeUsedByPublishedProduct);
        Assert.Equal(CreatedAtUtc, universe.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, universe.UpdatedAtUtc);
        Assert.Equal("admin-1", universe.CreatedBy);
        Assert.Equal("admin-1", universe.UpdatedBy);
        Assert.Equal(1, universe.Version);
    }

    [Fact]
    public void AdminFactoryCreatesUniverseWithRequiredLogoAtVersionOne()
    {
        var logo = Media("admin-create");

        var universe = Universe.CreateWithLogo(
            UniverseId,
            "มาร์เวล",
            "Marvel",
            CatalogSlug.Create("marvel"),
            logo,
            CreatedAtUtc,
            "admin-1");

        Assert.Equal(logo, universe.Logo);
        Assert.True(universe.CanBeUsedByPublishedProduct);
        Assert.Equal(1, universe.Version);
    }

    [Fact]
    public void NamesEnforceTrimmedAndNormalizedCentralLimitsWithoutMutation()
    {
        var exactLimit = new string('a', CatalogReferenceLimits.NameLength);
        var universe = CreateUniverse(displayName: exactLimit, englishName: exactLimit);

        Assert.Equal(CatalogReferenceLimits.NameLength, universe.DisplayName.Length);
        AssertRule(
            CatalogReferenceRule.NameTooLong,
            () => CreateUniverse(englishName: new string('a', CatalogReferenceLimits.NameLength + 1)));
        AssertRule(
            CatalogReferenceRule.NormalizedNameTooLong,
            () => CreateUniverse(englishName: new string('\uFB03', 67)));
    }

    [Fact]
    public void AtomicAdminUpdateChangesDetailsAndLogoWithOneVersionIncrement()
    {
        var universe = Universe.CreateWithLogo(
            UniverseId,
            "มาร์เวล",
            "Marvel",
            CatalogSlug.Create("marvel"),
            Media("first"),
            CreatedAtUtc,
            "admin-1");
        var replacement = Media("replacement");

        universe.UpdateDetailsWithLogo(
            "ดีซี",
            "DC",
            CatalogSlug.Create("dc"),
            replacement,
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal("ดีซี", universe.DisplayName);
        Assert.Equal("DC", universe.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("dc"), universe.Slug);
        Assert.Equal(replacement, universe.Logo);
        Assert.Equal(2, universe.Version);
    }

    [Fact]
    public void AtomicAdminUpdateNoOpAndStaleVersionLeaveEveryFieldUnchanged()
    {
        var universe = Universe.CreateWithLogo(
            UniverseId,
            "มาร์เวล",
            "Marvel",
            CatalogSlug.Create("marvel"),
            Media("first"),
            CreatedAtUtc,
            "admin-1");
        var before = Snapshot(universe);

        universe.UpdateDetailsWithLogo(
            universe.DisplayName,
            universe.EnglishName,
            universe.Slug,
            replacementLogo: null,
            expectedVersion: universe.Version,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal(before, Snapshot(universe));
        AssertRule(
            CatalogReferenceRule.ConcurrencyVersionMismatch,
            () => universe.UpdateDetailsWithLogo(
                "ใหม่",
                "New",
                CatalogSlug.Create("new"),
                Media("new"),
                expectedVersion: universe.Version + 1,
                CreatedAtUtc.AddMinutes(2),
                "admin-3"));
        Assert.Equal(before, Snapshot(universe));
    }

    [Fact]
    public void VersionedArchiveAdvancesOnceAndRejectsStaleWithoutMutation()
    {
        var stale = CreateUniverse();
        var staleBefore = Snapshot(stale);
        AssertRule(
            CatalogReferenceRule.ConcurrencyVersionMismatch,
            () => stale.Archive(2, CreatedAtUtc.AddMinutes(1), "admin-2"));
        Assert.Equal(staleBefore, Snapshot(stale));

        var universe = CreateUniverse();
        universe.Archive(1, CreatedAtUtc.AddMinutes(1), "admin-2");

        Assert.Equal(2, universe.Version);
        Assert.Equal(CatalogReferenceStatus.Archived, universe.Status);
    }

    [Fact]
    public void FactoryRejectsInvalidIdentityNameSlugAndAuditWithStableRules()
    {
        AssertRule(CatalogReferenceRule.IdentityRequired, () => CreateUniverse(id: Guid.Empty));
        AssertRule(CatalogReferenceRule.NameRequired, () => CreateUniverse(displayName: " "));
        AssertRule(CatalogReferenceRule.NameRequired, () => CreateUniverse(englishName: " "));
        AssertRule(
            CatalogReferenceRule.SlugInvalid,
            () => CreateUniverse(slug: (CatalogSlug?)default(CatalogSlug)));
        AssertRule(
            CatalogReferenceRule.AuditInstantMustBeUtc,
            () => CreateUniverse(createdAtUtc: CreatedAtUtc.ToOffset(TimeSpan.FromHours(7))));
        AssertRule(CatalogReferenceRule.AuditActorRequired, () => CreateUniverse(actor: " "));
    }

    [Fact]
    public void SeedsAreExactFlatDeterministicDefinitionsAndFreshReadOnlyValues()
    {
        var first = UniverseSeeds.All;
        var second = UniverseSeeds.All;

        Assert.Equal(3, first.Count);
        Assert.NotSame(first, second);
        Assert.Collection(
            first,
            marvel => AssertSeed(
                marvel,
                "20000000-0000-0000-0000-000000000001",
                "Marvel",
                "MARVEL",
                "marvel"),
            dc => AssertSeed(
                dc,
                "20000000-0000-0000-0000-000000000002",
                "DC",
                "DC",
                "dc"),
            unknown => AssertSeed(
                unknown,
                "20000000-0000-0000-0000-000000000003",
                "Unknown",
                "UNKNOWN",
                "unknown"));

        var firstAggregate = Universe.Create(
            first[0].Id,
            first[0].DisplayName,
            first[0].EnglishName,
            CatalogSlug.Create(first[0].Slug),
            first[0].CreatedAtUtc,
            first[0].CreatedBy);
        Assert.False(firstAggregate.CanBeUsedByPublishedProduct);
    }

    [Fact]
    public void SeedDefinitionsAreImmutableCopiesAndRejectDuplicateIdentityOrSlug()
    {
        var original = UniverseSeeds.All[0];
        var changedCopy = original with { DisplayName = "Changed" };

        Assert.Equal("Marvel", UniverseSeeds.All[0].DisplayName);
        Assert.Equal("Changed", changedCopy.DisplayName);

        var duplicateIdException = Assert.Throws<CatalogReferenceRuleException>(() =>
            UniverseSeeds.Validate(
            [
                original,
                original with { Slug = "different" },
            ]));
        Assert.Equal(CatalogReferenceRule.SeedIdentityDuplicate, duplicateIdException.Rule);

        var duplicateSlugException = Assert.Throws<CatalogReferenceRuleException>(() =>
            UniverseSeeds.Validate(
            [
                original,
                original with { Id = Guid.NewGuid() },
            ]));
        Assert.Equal(CatalogReferenceRule.SeedSlugDuplicate, duplicateSlugException.Rule);
    }

    [Fact]
    public void LogoAndDetailsMutationUpdateEligibilityNormalizationAndAudit()
    {
        var universe = CreateUniverse();
        var logo = Media("marvel");

        universe.AttachOrReplaceLogo(logo, CreatedAtUtc, "admin-2");
        universe.UpdateDetails(
            "  ดีซี\u2003คอมิกส์ ",
            "ＤC  Comics",
            CatalogSlug.Create("dc-comics"),
            CreatedAtUtc.AddMinutes(1),
            "admin-3");

        Assert.Equal(logo, universe.Logo);
        Assert.True(universe.CanBeUsedByPublishedProduct);
        Assert.Equal("ดีซี คอมิกส์", universe.NormalizedDisplayName);
        Assert.Equal("DC COMICS", universe.NormalizedEnglishName);
        Assert.Equal(CatalogSlug.Create("dc-comics"), universe.Slug);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), universe.UpdatedAtUtc);
        Assert.Equal("admin-3", universe.UpdatedBy);
    }

    [Theory]
    [InlineData("details", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("details", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("details", "actor", CatalogReferenceRule.AuditActorRequired)]
    [InlineData("logo", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("logo", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("logo", "actor", CatalogReferenceRule.AuditActorRequired)]
    [InlineData("archive", "nonUtc", CatalogReferenceRule.AuditInstantMustBeUtc)]
    [InlineData("archive", "backwards", CatalogReferenceRule.AuditTimeWentBackwards)]
    [InlineData("archive", "actor", CatalogReferenceRule.AuditActorRequired)]
    public void MutationRejectsInvalidAuditWithoutChangingState(
        string mutation,
        string invalidField,
        CatalogReferenceRule expectedRule)
    {
        var universe = CreateUniverse();
        var before = Snapshot(universe);
        var changedAtUtc = invalidField switch
        {
            "nonUtc" => CreatedAtUtc.ToOffset(TimeSpan.FromHours(7)),
            "backwards" => CreatedAtUtc.AddTicks(-1),
            _ => CreatedAtUtc,
        };
        var actor = invalidField == "actor" ? " " : "admin-2";

        var exception = Assert.Throws<CatalogReferenceRuleException>(() => mutation switch
        {
            "details" => Invoke(() => universe.UpdateDetails(
                "ชื่อใหม่",
                "New Universe",
                CatalogSlug.Create("new-universe"),
                changedAtUtc,
                actor)),
            "logo" => Invoke(() => universe.AttachOrReplaceLogo(Media("new"), changedAtUtc, actor)),
            _ => Invoke(() => universe.Archive(changedAtUtc, actor)),
        });

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(before, Snapshot(universe));
    }

    [Fact]
    public void ArchiveIsTerminalAndDoesNotDeleteLogo()
    {
        var universe = CreateUniverse();
        var logo = Media("marvel");
        universe.AttachOrReplaceLogo(logo, CreatedAtUtc, "admin-1");

        universe.Archive(CreatedAtUtc.AddHours(1), "admin-2");

        Assert.Equal(CatalogReferenceStatus.Archived, universe.Status);
        Assert.Equal(CreatedAtUtc.AddHours(1), universe.ArchivedAtUtc);
        Assert.Equal("admin-2", universe.ArchivedBy);
        Assert.Equal(logo, universe.Logo);
        Assert.False(universe.CanBeUsedByPublishedProduct);
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => universe.UpdateDetails(
                "ใหม่",
                "New",
                CatalogSlug.Create("new"),
                CreatedAtUtc.AddHours(2),
                "admin-3"));
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => universe.AttachOrReplaceLogo(Media("new"), CreatedAtUtc.AddHours(2), "admin-3"));
        AssertRule(
            CatalogReferenceRule.ReferenceArchived,
            () => universe.Archive(CreatedAtUtc.AddHours(2), "admin-3"));
    }

    [Fact]
    public void UniverseHasNoPublicInvalidConstructorOrPublicSetters()
    {
        Assert.Empty(typeof(Universe).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            typeof(Universe).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    private static Universe CreateUniverse(
        Guid? id = null,
        string displayName = "มาร์เวล",
        string englishName = "Marvel",
        CatalogSlug? slug = null,
        DateTimeOffset? createdAtUtc = null,
        string actor = "admin-1") =>
        Universe.Create(
            id ?? UniverseId,
            displayName,
            englishName,
            slug ?? CatalogSlug.Create("marvel"),
            createdAtUtc ?? CreatedAtUtc,
            actor);

    private static CatalogMediaReference Media(string name) =>
        CatalogMediaReference.Create(
            $"universes/{name}.webp",
            $"/media/universes/{name}.webp",
            $"โลโก้ {name}");

    private static void AssertSeed(
        UniverseSeedDefinition seed,
        string id,
        string name,
        string normalizedName,
        string slug)
    {
        Assert.Equal(Guid.Parse(id), seed.Id);
        Assert.Equal(name, seed.DisplayName);
        Assert.Equal(name, seed.EnglishName);
        Assert.Equal(normalizedName, seed.NormalizedDisplayName);
        Assert.Equal(normalizedName, seed.NormalizedEnglishName);
        Assert.Equal(slug, seed.Slug);
        Assert.Equal(CatalogReferenceStatus.Active, seed.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), seed.CreatedAtUtc);
        Assert.Equal("system:catalog-seed", seed.CreatedBy);
    }

    private static object Snapshot(Universe universe) => new
    {
        universe.DisplayName,
        universe.NormalizedDisplayName,
        universe.EnglishName,
        universe.NormalizedEnglishName,
        universe.Slug,
        universe.Logo,
        universe.Status,
        universe.UpdatedAtUtc,
        universe.UpdatedBy,
        universe.ArchivedAtUtc,
        universe.ArchivedBy,
        universe.Version,
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
