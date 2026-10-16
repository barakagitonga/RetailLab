using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncChangeFeedEntryConfiguration : IEntityTypeConfiguration<SyncChangeFeedEntry>
{
    public void Configure(EntityTypeBuilder<SyncChangeFeedEntry> builder)
    {
        builder.ToTable("SyncChangeFeedEntries", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SyncChangeFeedEntries_ServerRevision",
                "ServerRevision >= 0");
        });

        builder.HasKey(entry => entry.Sequence);
        builder.Property(entry => entry.Sequence)
            .ValueGeneratedOnAdd();

        builder.Property(entry => entry.ProductId).IsRequired();
        builder.HasIndex(entry => entry.ProductId);
        builder.HasIndex(entry => new { entry.ProductId, entry.ServerRevision })
            .IsUnique();

        builder.Property(entry => entry.ChangeKind).IsRequired();

        builder.Property(entry => entry.ServerRevision)
            .IsRequired();

        builder.Property(entry => entry.Payload).IsRequired();

        builder.Property(entry => entry.OccurredAtUtc).IsRequired();
    }
}
