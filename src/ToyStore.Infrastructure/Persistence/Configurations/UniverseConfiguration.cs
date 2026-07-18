using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class UniverseConfiguration : IEntityTypeConfiguration<Universe>
{
    public void Configure(EntityTypeBuilder<Universe> builder)
    {
        builder.ToTable("Universes", table =>
        {
            table.HasCheckConstraint(
                "CK_Universes_Slug_Format",
                "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint(
                "CK_Universes_Logo_AllNullOrPresent",
                "(\"LogoStorageKey\" IS NULL AND \"LogoPublicRelativeUrl\" IS NULL AND \"LogoAltText\" IS NULL) OR (\"LogoStorageKey\" IS NOT NULL AND \"LogoStorageKey\" ~ '[^[:space:]]' AND \"LogoPublicRelativeUrl\" IS NOT NULL AND \"LogoPublicRelativeUrl\" ~ '[^[:space:]]' AND \"LogoAltText\" IS NOT NULL AND \"LogoAltText\" ~ '[^[:space:]]')");
        });
        builder.HasKey(universe => universe.Id);
        builder.Property(universe => universe.DisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(universe => universe.NormalizedDisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(universe => universe.EnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(universe => universe.NormalizedEnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(universe => universe.Slug).HasConversion(CatalogConfiguration.SlugConverter).HasMaxLength(CatalogConfiguration.SlugLength).IsRequired();
        builder.Property(universe => universe.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(universe => universe.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(universe => universe.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(universe => universe.ArchivedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(universe => universe.CreatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(universe => universe.UpdatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(universe => universe.ArchivedBy).HasMaxLength(CatalogConfiguration.ActorLength);
        builder.Property(universe => universe.Version)
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Ignore(universe => universe.CanBeUsedByPublishedProduct);
        builder.HasIndex(universe => universe.Slug).IsUnique().HasDatabaseName("UX_Universes_Slug");
        builder.HasIndex(universe => universe.NormalizedDisplayName).IsUnique().HasDatabaseName("UX_Universes_NormalizedDisplayName");
        builder.HasIndex(universe => universe.NormalizedEnglishName).IsUnique().HasDatabaseName("UX_Universes_NormalizedEnglishName");
        builder.OwnsOne(universe => universe.Logo, media =>
        {
            media.Property(value => value.StorageKey).HasColumnName("LogoStorageKey").HasMaxLength(CatalogConfiguration.StorageKeyLength);
            media.Property(value => value.PublicRelativeUrl).HasColumnName("LogoPublicRelativeUrl").HasMaxLength(CatalogConfiguration.UrlLength);
            media.Property(value => value.AltText).HasColumnName("LogoAltText").HasMaxLength(CatalogConfiguration.AltTextLength);
        });

        builder.HasData(UniverseSeeds.All.Select(seed => new
        {
            seed.Id,
            seed.DisplayName,
            seed.NormalizedDisplayName,
            seed.EnglishName,
            seed.NormalizedEnglishName,
            Slug = CatalogSlug.Create(seed.Slug),
            seed.Status,
            seed.CreatedAtUtc,
            seed.CreatedBy,
            UpdatedAtUtc = seed.CreatedAtUtc,
            UpdatedBy = seed.CreatedBy,
            ArchivedAtUtc = (DateTimeOffset?)null,
            ArchivedBy = (string?)null,
            Version = 1L,
        }));
    }
}
