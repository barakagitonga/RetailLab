using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        RetailLabDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Products.AddRange(
            new Product("AUR-100", "Aurora insulated travel mug", 24.95m, 18),
            new Product("BRI-210", "Brindle recycled canvas tote", 16.50m, 25),
            new Product("CIR-320", "Cirrus compact desk lamp", 39.99m, 10),
            new Product("DOV-430", "Dovetail bamboo notebook", 8.75m, 40),
            new Product("EMBER-540", "Emberline wireless speaker", 59.00m, 7));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
