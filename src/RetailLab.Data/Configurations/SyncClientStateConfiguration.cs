using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncClientStateConfiguration : IEntityTypeConfiguration<SyncClientState>
{
    public void Configure(EntityTypeBuilder<SyncClientState> builder)
    {
        builder.ToTable("SyncClientStates", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_SyncClientStates_Singleton", "Id = 1");
            tableBuilder.HasCheckConstraint("CK_SyncClientStates_LastPulledSequence", "LastPulledSequence >= 0");
        });

        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).ValueGeneratedNever();

        builder.Property(state => state.DeviceId).IsRequired();

        builder.Property(state => state.PairedServerId)
            .HasMaxLength(SyncClientState.MaximumServerIdLength);

        builder.Property(state => state.LastPulledSequence)
            .IsRequired();

        builder.Property(state => state.PairedAtUtc);
        builder.Property(state => state.LastSyncAtUtc);
    }
}
