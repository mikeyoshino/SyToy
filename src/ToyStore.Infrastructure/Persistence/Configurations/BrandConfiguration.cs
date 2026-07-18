using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands", table =>
        {
            table.HasCheckConstraint(
                "CK_Brands_Slug_Format",
                "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint(
                "CK_Brands_Image_AllNullOrPresent",
                "(\"ImageStorageKey\" IS NULL AND \"ImagePublicRelativeUrl\" IS NULL AND \"ImageAltText\" IS NULL) OR (\"ImageStorageKey\" IS NOT NULL AND \"ImageStorageKey\" ~ '[^[:space:]]' AND \"ImagePublicRelativeUrl\" IS NOT NULL AND \"ImagePublicRelativeUrl\" ~ '[^[:space:]]' AND \"ImageAltText\" IS NOT NULL AND \"ImageAltText\" ~ '[^[:space:]]')");
        });
        builder.HasKey(brand => brand.Id);
        ConfigureCommon(builder);
        builder.OwnsOne(brand => brand.Image, media => ConfigureMedia(media, "Image"));
    }

    private static void ConfigureCommon(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(brand => brand.DisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(brand => brand.NormalizedDisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(brand => brand.EnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(brand => brand.NormalizedEnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(brand => brand.Slug).HasConversion(CatalogConfiguration.SlugConverter).HasMaxLength(CatalogConfiguration.SlugLength).IsRequired();
        builder.Property(brand => brand.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(brand => brand.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(brand => brand.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(brand => brand.ArchivedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(brand => brand.CreatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(brand => brand.UpdatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(brand => brand.ArchivedBy).HasMaxLength(CatalogConfiguration.ActorLength);
        builder.Property(brand => brand.Version)
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Ignore(brand => brand.CanBeUsedByPublishedProduct);
        builder.HasIndex(brand => brand.Slug).IsUnique().HasDatabaseName("UX_Brands_Slug");
        builder.HasIndex(brand => brand.NormalizedDisplayName).IsUnique().HasDatabaseName("UX_Brands_NormalizedDisplayName");
        builder.HasIndex(brand => brand.NormalizedEnglishName).IsUnique().HasDatabaseName("UX_Brands_NormalizedEnglishName");
    }

    internal static void ConfigureMedia(
        OwnedNavigationBuilder<Brand, CatalogMediaReference> media,
        string prefix)
    {
        media.Property(value => value.StorageKey).HasColumnName($"{prefix}StorageKey").HasMaxLength(CatalogConfiguration.StorageKeyLength);
        media.Property(value => value.PublicRelativeUrl).HasColumnName($"{prefix}PublicRelativeUrl").HasMaxLength(CatalogConfiguration.UrlLength);
        media.Property(value => value.AltText).HasColumnName($"{prefix}AltText").HasMaxLength(CatalogConfiguration.AltTextLength);
    }
}
