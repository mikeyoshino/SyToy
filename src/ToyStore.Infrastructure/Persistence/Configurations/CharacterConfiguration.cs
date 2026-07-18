using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("Characters");
        builder.HasKey(character => character.Id);
        builder.Property(character => character.Name).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(character => character.NormalizedName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Ignore(character => character.Identity);
        builder.HasIndex(character => new { character.UniverseId, character.NormalizedName })
            .IsUnique()
            .HasDatabaseName("UX_Characters_UniverseId_NormalizedName");
        builder.HasOne<Universe>().WithMany().HasForeignKey(character => character.UniverseId).OnDelete(DeleteBehavior.Restrict);
    }
}
