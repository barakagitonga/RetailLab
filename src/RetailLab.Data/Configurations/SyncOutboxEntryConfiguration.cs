using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncOutboxEntryConfiguration : IEntityTypeConfiguration<SyncOutboxEntry>
{
    public void Configure(EntityTypeBuilder<SyncOutboxEntry> builder)
    {
        builder.ToTable("SyncOutboxEntries", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SyncOutboxEntries_BaseServerRevision",
                "BaseServerRevision IS NULL OR BaseServerRevision >= 0");
        });

        builder.HasKey(entry => entry.LocalSequence);
        builder.Property(entry => entry.LocalSequence)
            .ValueGeneratedOnAdd();

        builder.Property(entry => entry.OperationId).IsRequired();
        builder.HasIndex(entry => entry.OperationId).IsUnique();

        builder.Property(entry => entry.ProductId).IsRequired();
        builder.HasIndex(entry => entry.ProductId);

        builder.Property(entry => entry.Kind).IsRequired();
        builder.Property(entry => entry.Payload).IsRequired();

        builder.Property(entry => entry.BaseServerRevision);
        builder.Property(entry => entry.PredecessorOperationId);

        builder.Property(entry => entry.CreatedAtUtc).IsRequired();

        builder.Property(entry => entry.State).IsRequired();
        builder.HasIndex(entry => new { entry.State, entry.LocalSequence });

        builder.Property(entry => entry.RecordedResult);
    }
}
