using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public class RetailLabDbContext : IdentityDbContext<IdentityUser>
{
    public RetailLabDbContext(DbContextOptions<RetailLabDbContext> options)
        : base(options)
    {
    }

    protected RetailLabDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<BasketItem> BasketItems => Set<BasketItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();

    public DbSet<SyncClientState> SyncClientStates => Set<SyncClientState>();

    public DbSet<SyncOutboxEntry> SyncOutboxEntries => Set<SyncOutboxEntry>();

    public DbSet<SyncInboxReceipt> SyncInboxReceipts => Set<SyncInboxReceipt>();

    public DbSet<SyncChangeFeedEntry> SyncChangeFeedEntries => Set<SyncChangeFeedEntry>();

    public DbSet<SyncServerProperties> SyncServerProperties => Set<SyncServerProperties>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetailLabDbContext).Assembly);
    }
}
