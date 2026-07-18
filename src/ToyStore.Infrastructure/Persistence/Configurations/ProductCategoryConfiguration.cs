using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(category => category.Code).IsUnique().HasDatabaseName("UX_ProductCategories_Code");
        builder.HasData(ProductCategorySeeds.All.Select(category => new
        {
            category.Id,
            category.Code,
        }));
    }
}
