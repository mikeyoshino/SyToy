using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class MediaCleanupEntryConfiguration
    : IEntityTypeConfiguration<MediaCleanupEntry>
{
    public void Configure(EntityTypeBuilder<MediaCleanupEntry> builder)
    {
        builder.ToTable("MediaCleanupEntries", table =>
        {
            table.HasCheckConstraint(
                "CK_MediaCleanupEntries_AttemptCount_Positive",
                "\"AttemptCount\" > 0");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.StorageKey)
            .HasMaxLength(CatalogConfiguration.StorageKeyLength)
            .IsRequired();
        builder.Property(entry => entry.Reason)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(entry => entry.EntityType)
            .HasMaxLength(CatalogConfiguration.ActorLength)
            .IsRequired();
        builder.Property(entry => entry.EntityId).IsRequired();
        builder.Property(entry => entry.FirstObservedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(entry => entry.LastAttemptAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(entry => entry.AttemptCount).IsRequired();
        builder.Property(entry => entry.ResolvedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(entry => entry.StorageKey)
            .IsUnique()
            .HasFilter("\"ResolvedAtUtc\" IS NULL")
            .HasDatabaseName("UX_MediaCleanupEntries_Unresolved_StorageKey");
    }
}
