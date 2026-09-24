using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task InitializerAndServices_PersistSeedBookmarkOrderAndStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
            await DatabaseInitializer.InitializeAsync(initializationContext);
            Assert.Equal(5, await initializationContext.Products.CountAsync());
        }

        var now = new DateTimeOffset(2026, 9, 24, 11, 0, 0, TimeSpan.Zero);

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(commandContext);
            await new BookmarkService(repository, new TestTimeProvider(now))
                .AddAsync("customer-demo-001", "AUR-100");

            await new OrderService(repository, new TestTimeProvider(now))
                .PlaceAsync(
                    "customer-demo-001",
                    [new OrderItemRequest("AUR-100", 2)]);
        }

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        var bookmark = Assert.Single(
            await verificationRepository.GetBookmarksAsync("customer-demo-001"));
        Assert.Equal("AUR-100", bookmark.Product.Sku);

        var order = Assert.Single(
            await verificationRepository.GetOrdersAsync("customer-demo-001"));
        Assert.Equal(49.90m, order.Total);

        var product = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(product);
        Assert.Equal(16, product.StockQuantity);
    }
}
