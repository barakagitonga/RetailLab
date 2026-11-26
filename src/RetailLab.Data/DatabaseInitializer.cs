using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        RetailLabDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await MigrateAsync(dbContext, cancellationToken);
        await SeedIfEmptyAsync(dbContext, cancellationToken);
    }

    public static Task MigrateAsync(
        RetailLabDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }

    public static async Task SeedIfEmptyAsync(
        RetailLabDbContext dbContext,
        CancellationToken cancellationToken = default)
    {

        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Products.AddRange(
            new Product("AUR-100", "Aurora insulated travel mug", 24.95m, 18),
            new Product("BRI-210", "Brindle recycled canvas tote", 16.50m, 25),
            new Product("CIR-320", "Cirrus compact desk lamp", 39.99m, 10),
            new Product("DOV-430", "Dovetail bamboo notebook", 8.75m, 40),
            new Product("EMBER-540", "Emberline wireless speaker", 59.00m, 7),
            new Product("FLX-610", "Flexline stainless water bottle", 19.95m, 30),
            new Product("GLD-720", "Gilded ceramic pour-over set", 34.50m, 12),
            new Product("HAV-830", "Haven weighted desk blanket", 49.00m, 8),
            new Product("IND-940", "Coastal Stripe Cotton Towel Set", 22.75m, 22),
            new Product("JUN-150", "Juniper scented soy candle", 14.25m, 35));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
