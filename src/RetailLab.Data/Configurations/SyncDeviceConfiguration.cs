using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncDeviceConfiguration : IEntityTypeConfiguration<SyncDevice>
{
    public void Configure(EntityTypeBuilder<SyncDevice> builder)
    {
        builder.ToTable("SyncDevices", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SyncDevices_Role",
                "Role = 'staff'");
            tableBuilder.HasCheckConstraint(
                "CK_SyncDevices_Status",
                "Status IN ('Active', 'Revoked')");
        });

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).ValueGeneratedNever();
        builder.Property(device => device.DisplayName)
            .HasMaxLength(SyncDevice.MaximumDisplayNameLength)
            .IsRequired();
        builder.Property(device => device.StaffDisplayName)
            .HasMaxLength(SyncDevice.MaximumStaffDisplayNameLength)
            .IsRequired();
        builder.Property(device => device.Role)
            .HasMaxLength(SyncDevice.MaximumRoleLength)
            .IsRequired();
        builder.Property(device => device.KeyHash).IsRequired();
        builder.Property(device => device.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(device => device.CreatedAtUtc).IsRequired();
        builder.Property(device => device.RevokedAtUtc);
        builder.Property(device => device.LastSeenAtUtc);
    }
}
