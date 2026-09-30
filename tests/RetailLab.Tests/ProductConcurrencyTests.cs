using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class ProductConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Version_StartsAtZeroAndBumpsOnEverySuccessfulMutation()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        Assert.Equal(0, product.Version);

        product.UpdateDetails("Revised product", 6m);
        Assert.Equal(1, product.Version);

        product.Archive(Now);
        Assert.Equal(2, product.Version);

        product.Unarchive();
        Assert.Equal(3, product.Version);

        product.AdjustStock(2);
        Assert.Equal(4, product.Version);

        product.ReduceStock(1);
        Assert.Equal(5, product.Version);
    }

    [Fact]
    public void Version_StaysPutWhenMutationFails()
    {
        var product = new Product("SKU-1", "Product", 5m, 2);
        product.UpdateDetails("Revised product", 6m);
        Assert.Equal(1, product.Version);

        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(0));
        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(-3));
        Assert.Equal(1, product.Version);

        product.Archive(Now);
        Assert.Equal(2, product.Version);
        Assert.Throws<BusinessRuleException>(() => product.UpdateDetails("Changed", 7m));
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public async Task StaffUpdate_Race_LoserGetsProductConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        // The second editor loads first and goes stale while the first saves.
        await using var contextB = new RetailLabDbContext(options);
        var repositoryB = new EfRetailRepository(contextB);
        Assert.NotNull(await repositoryB.FindProductBySkuAsync("AUR-100"));

        await using (var contextA = new RetailLabDbContext(options))
        {
            await new ProductService(new EfRetailRepository(contextA), new TestTimeProvider(Now))
                .UpdateDetailsAsync("AUR-100", "Revised mug", 30.00m);
        }

        await Assert.ThrowsAsync<ProductConflictException>(
            () => new ProductService(repositoryB, new TestTimeProvider(Now))
                .UpdateDetailsAsync("AUR-100", "Stale mug", 1.00m));

        await using var verificationContext = new RetailLabDbContext(options);
        var winner = await new EfRetailRepository(verificationContext)
            .FindProductBySkuAsync("AUR-100");
        Assert.NotNull(winner);
        Assert.Equal("Revised mug", winner.Description);
        Assert.Equal(30.00m, winner.Price);
    }

    [Fact]
    public async Task Adjust_Race_LoserGetsProductConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        // The second adjustment loads first and goes stale while the first saves.
        await using var contextB = new RetailLabDbContext(options);
        var repositoryB = new EfRetailRepository(contextB);
        Assert.NotNull(await repositoryB.FindProductBySkuAsync("AUR-100"));

        await using (var contextA = new RetailLabDbContext(options))
        {
            await new InventoryService(new EfRetailRepository(contextA), new TestTimeProvider(Now))
                .AdjustAsync("AUR-100", 5, "Restock", "staff-demo-01");
        }

        await Assert.ThrowsAsync<ProductConflictException>(
            () => new InventoryService(repositoryB, new TestTimeProvider(Now))
                .AdjustAsync("AUR-100", -2, "Damaged items", "staff-demo-01"));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);
        var product = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(product);
        Assert.Equal(23, product.StockQuantity);

        // Only the winning adjustment was recorded.
        var history = await verificationRepository.GetAdjustmentsAsync(product.Id);
        var adjustment = Assert.Single(history);
        Assert.Equal(5, adjustment.QuantityChange);
        Assert.Equal("Restock", adjustment.Reason);
    }

    [Fact]
    public async Task ConsoleOrder_Race_LoserGetsProductConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var seedContext = new RetailLabDbContext(options))
        {
            await new ProductService(new EfRetailRepository(seedContext), new TestTimeProvider(Now))
                .CreateAsync("LAST-2", "Last units product", 9.99m, 2);
        }

        // The second order loads first and goes stale while the first saves.
        // It validates against stale stock, so the version check — not the
        // stock check — must reject it.
        await using var contextB = new RetailLabDbContext(options);
        var repositoryB = new EfRetailRepository(contextB);
        Assert.NotNull(await repositoryB.FindProductBySkuAsync("LAST-2"));

        await using (var contextA = new RetailLabDbContext(options))
        {
            await new OrderService(new EfRetailRepository(contextA), new TestTimeProvider(Now))
                .PlaceAsync("customer-a", [new OrderItemRequest("LAST-2", 2)]);
        }

        await Assert.ThrowsAsync<ProductConflictException>(
            () => new OrderService(repositoryB, new TestTimeProvider(Now))
                .PlaceAsync("customer-b", [new OrderItemRequest("LAST-2", 1)]));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);
        var product = await verificationRepository.FindProductBySkuAsync("LAST-2");
        Assert.NotNull(product);
        Assert.Equal(0, product.StockQuantity);
        Assert.Single(await verificationRepository.GetOrdersAsync("customer-a"));
        Assert.Empty(await verificationRepository.GetOrdersAsync("customer-b"));
    }

    [Fact]
    public async Task Migration_PreservesDataWithVersionZero()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        var products = await verificationRepository.GetProductsAsync(includeArchived: true);
        Assert.Equal(5, products.Count);
        var mug = products.Single(product => product.Sku == "AUR-100");
        Assert.Equal("Aurora insulated travel mug", mug.Description);
        Assert.Equal(24.95m, mug.Price);
        Assert.Equal(18, mug.StockQuantity);
        Assert.All(products, product => Assert.Equal(0, product.Version));

        await new InventoryService(verificationRepository, new TestTimeProvider(Now))
            .AdjustAsync("AUR-100", 1, "Recount", "staff-demo-01");

        var adjusted = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(adjusted);
        Assert.Equal(1, adjusted.Version);
        Assert.Equal(19, adjusted.StockQuantity);
    }
}
