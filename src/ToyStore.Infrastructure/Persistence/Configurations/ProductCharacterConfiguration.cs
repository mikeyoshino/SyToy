using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class ProductCharacterConfiguration : IEntityTypeConfiguration<ProductCharacter>
{
    public void Configure(EntityTypeBuilder<ProductCharacter> builder)
    {
        builder.ToTable("ProductCharacters");
        builder.HasKey(link => new { link.ProductId, link.CharacterId });
        builder.HasOne<Character>().WithMany().HasForeignKey(link => link.CharacterId).OnDelete(DeleteBehavior.Restrict);
    }
}
