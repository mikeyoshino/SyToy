namespace ToyStore.Domain.Catalog;

public sealed class Universe
{
    private Universe()
    {
        DisplayName = null!;
        NormalizedDisplayName = null!;
        EnglishName = null!;
        NormalizedEnglishName = null!;
        CreatedBy = null!;
        UpdatedBy = null!;
        Version = 1;
    }

    private Universe(
        Guid id,
        string displayName,
        string englishName,
        CatalogSlug slug,
        CatalogMediaReference? logo,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        Id = id;
        var preparedDisplayName = CatalogReferenceLimits.PrepareName(displayName);
        var preparedEnglishName = CatalogReferenceLimits.PrepareName(englishName);
        DisplayName = preparedDisplayName.Persisted;
        NormalizedDisplayName = preparedDisplayName.Normalized;
        EnglishName = preparedEnglishName.Persisted;
        NormalizedEnglishName = preparedEnglishName.Normalized;
        Slug = slug;
        Logo = logo;
        Status = CatalogReferenceStatus.Active;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = actor;
        UpdatedAtUtc = createdAtUtc;
        UpdatedBy = actor;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    public string NormalizedDisplayName { get; private set; }

    public string EnglishName { get; private set; }

    public string NormalizedEnglishName { get; private set; }

    public CatalogSlug Slug { get; private set; }

    public CatalogMediaReference? Logo { get; private set; }

    public CatalogReferenceStatus Status { get; private set; }

    public bool CanBeUsedByPublishedProduct =>
        Status == CatalogReferenceStatus.Active && Logo is not null;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public string? ArchivedBy { get; private set; }

    public long Version { get; private set; }

    public static Universe Create(
        Guid id,
        string displayName,
        string englishName,
        CatalogSlug slug,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        EnsureIdentity(id);
        _ = CatalogReferenceLimits.PrepareName(displayName);
        _ = CatalogReferenceLimits.PrepareName(englishName);
        EnsureSlug(slug);
        ValidateInitialAudit(createdAtUtc, actor);
        return new Universe(id, displayName, englishName, slug, null, createdAtUtc, actor);
    }

    public static Universe CreateWithLogo(
        Guid id,
        string displayName,
        string englishName,
        CatalogSlug slug,
        CatalogMediaReference logo,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        EnsureIdentity(id);
        _ = CatalogReferenceLimits.PrepareName(displayName);
        _ = CatalogReferenceLimits.PrepareName(englishName);
        EnsureSlug(slug);
        EnsureMedia(logo);
        ValidateInitialAudit(createdAtUtc, actor);
        return new Universe(id, displayName, englishName, slug, logo, createdAtUtc, actor);
    }

    public void UpdateDetails(
        string displayName,
        string englishName,
        CatalogSlug slug,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        EnsureActive();
        var preparedDisplayName = CatalogReferenceLimits.PrepareName(displayName);
        var preparedEnglishName = CatalogReferenceLimits.PrepareName(englishName);
        EnsureSlug(slug);
        ValidateAudit(changedAtUtc, actor);

        if (HasSameDetails(preparedDisplayName, preparedEnglishName, slug))
        {
            return;
        }

        EnsureVersionCanAdvance();

        DisplayName = preparedDisplayName.Persisted;
        NormalizedDisplayName = preparedDisplayName.Normalized;
        EnglishName = preparedEnglishName.Persisted;
        NormalizedEnglishName = preparedEnglishName.Normalized;
        Slug = slug;
        ApplyMutationAudit(changedAtUtc, actor);
    }

    public void UpdateDetailsWithLogo(
        string displayName,
        string englishName,
        CatalogSlug slug,
        CatalogMediaReference? replacementLogo,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        EnsureActive();
        EnsureExpectedVersion(expectedVersion);
        var preparedDisplayName = CatalogReferenceLimits.PrepareName(displayName);
        var preparedEnglishName = CatalogReferenceLimits.PrepareName(englishName);
        EnsureSlug(slug);
        var resultingLogo = replacementLogo ?? Logo;
        EnsureMedia(resultingLogo);
        ValidateAudit(changedAtUtc, actor);

        if (HasSameDetails(preparedDisplayName, preparedEnglishName, slug)
            && Equals(Logo, resultingLogo))
        {
            return;
        }

        EnsureVersionCanAdvance();
        DisplayName = preparedDisplayName.Persisted;
        NormalizedDisplayName = preparedDisplayName.Normalized;
        EnglishName = preparedEnglishName.Persisted;
        NormalizedEnglishName = preparedEnglishName.Normalized;
        Slug = slug;
        Logo = resultingLogo;
        ApplyMutationAudit(changedAtUtc, actor);
    }

    public void AttachOrReplaceLogo(
        CatalogMediaReference logo,
        DateTimeOffset changedAtUtc,
        string actor)
    {
        EnsureActive();
        EnsureMedia(logo);

        ValidateAudit(changedAtUtc, actor);
        if (Equals(Logo, logo))
        {
            return;
        }

        EnsureVersionCanAdvance();
        Logo = logo;
        ApplyMutationAudit(changedAtUtc, actor);
    }

    public void Archive(DateTimeOffset archivedAtUtc, string actor)
    {
        ArchiveCore(null, archivedAtUtc, actor);
    }

    public void Archive(long expectedVersion, DateTimeOffset archivedAtUtc, string actor)
    {
        ArchiveCore(expectedVersion, archivedAtUtc, actor);
    }

    private void ArchiveCore(long? expectedVersion, DateTimeOffset archivedAtUtc, string actor)
    {
        EnsureActive();
        if (expectedVersion.HasValue)
        {
            EnsureExpectedVersion(expectedVersion.Value);
        }

        ValidateAudit(archivedAtUtc, actor);
        EnsureVersionCanAdvance();
        Status = CatalogReferenceStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
        ArchivedBy = actor;
        ApplyMutationAudit(archivedAtUtc, actor);
    }

    private static void EnsureIdentity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.IdentityRequired);
        }
    }

    private static void EnsureSlug(CatalogSlug slug)
    {
        if (!CatalogSlug.IsValid(slug.Value))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SlugInvalid);
        }
    }

    private static void EnsureMedia(CatalogMediaReference? logo)
    {
        if (logo is null)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.MediaRequired);
        }
    }

    private static void ValidateInitialAudit(DateTimeOffset changedAtUtc, string actor)
    {
        if (changedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.AuditInstantMustBeUtc);
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.AuditActorRequired);
        }
    }

    private void ValidateAudit(DateTimeOffset changedAtUtc, string actor)
    {
        ValidateInitialAudit(changedAtUtc, actor);
        if (changedAtUtc < UpdatedAtUtc)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.AuditTimeWentBackwards);
        }
    }

    private void EnsureActive()
    {
        if (Status != CatalogReferenceStatus.Active)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.ReferenceArchived);
        }
    }

    private bool HasSameDetails(
        CatalogReferenceLimits.PreparedCatalogName displayName,
        CatalogReferenceLimits.PreparedCatalogName englishName,
        CatalogSlug slug) =>
        DisplayName == displayName.Persisted
        && NormalizedDisplayName == displayName.Normalized
        && EnglishName == englishName.Persisted
        && NormalizedEnglishName == englishName.Normalized
        && Slug == slug;

    private void EnsureExpectedVersion(long expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new CatalogReferenceRuleException(
                CatalogReferenceRule.ConcurrencyVersionMismatch);
        }
    }

    private void EnsureVersionCanAdvance()
    {
        if (Version == long.MaxValue)
        {
            throw new CatalogReferenceRuleException(
                CatalogReferenceRule.ConcurrencyVersionExhausted);
        }
    }

    private void ApplyMutationAudit(DateTimeOffset changedAtUtc, string actor)
    {
        UpdatedAtUtc = changedAtUtc;
        UpdatedBy = actor;
        Version++;
    }
}
