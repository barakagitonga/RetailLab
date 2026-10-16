using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncServerPropertiesConfiguration
    : IEntityTypeConfiguration<SyncServerProperties>
{
    public void Configure(EntityTypeBuilder<SyncServerProperties> builder)
    {
        builder.ToTable("SyncServerProperties", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SyncServerProperties_Singleton",
                "Id = 1");
        });

        builder.HasKey(properties => properties.Id);
        builder.Property(properties => properties.Id).ValueGeneratedNever();

        builder.Property(properties => properties.ServerInstanceId).IsRequired();
        builder.HasIndex(properties => properties.ServerInstanceId).IsUnique();

        builder.Property(properties => properties.CreatedAtUtc).IsRequired();
    }
}
