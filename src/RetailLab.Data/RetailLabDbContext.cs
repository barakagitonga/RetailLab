using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public sealed class RetailLabDbContext(DbContextOptions<RetailLabDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetailLabDbContext).Assembly);
    }
}
