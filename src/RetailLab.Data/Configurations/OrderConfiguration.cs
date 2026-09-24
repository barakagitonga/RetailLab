using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);

        builder.Property(order => order.CustomerIdentifier)
            .HasMaxLength(CustomerIdentifier.MaximumLength)
            .IsRequired();

        builder.Property(order => order.PlacedAtUtc).IsRequired();
        builder.Ignore(order => order.Total);

        builder.HasIndex(order => new { order.CustomerIdentifier, order.PlacedAtUtc });

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
