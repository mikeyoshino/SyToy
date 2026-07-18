using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages", table =>
        {
            table.HasCheckConstraint("CK_ProductImages_SortOrder", "\"SortOrder\" >= 0");
            table.HasCheckConstraint("CK_ProductImages_PrimaryMatchesOrder", "\"IsPrimary\" = (\"SortOrder\" = 0)");
        });
        builder.HasKey(image => image.Id);
        builder.Property<Guid>("ProductId");
        builder.Property(image => image.StorageKey).HasMaxLength(CatalogConfiguration.StorageKeyLength).IsRequired();
        builder.Property(image => image.PublicRelativeUrl).HasMaxLength(CatalogConfiguration.UrlLength).IsRequired();
        builder.Property(image => image.AltText).HasMaxLength(CatalogConfiguration.AltTextLength).IsRequired();
        builder.HasIndex(image => image.StorageKey).IsUnique().HasDatabaseName("UX_ProductImages_StorageKey");
        builder.HasIndex("ProductId", nameof(ProductImage.SortOrder)).IsUnique().HasDatabaseName("UX_ProductImages_ProductId_SortOrder");
        builder.HasIndex("ProductId").IsUnique().HasFilter("\"IsPrimary\"").HasDatabaseName("UX_ProductImages_ProductId_Primary");
    }
}
