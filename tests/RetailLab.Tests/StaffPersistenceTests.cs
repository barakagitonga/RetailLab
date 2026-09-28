using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class StaffPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StaffWorkflow_PersistsProductChangesAndAdjustmentHistory()
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

        var stamp = new TestTimeProvider(Now);

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(commandContext);
            var products = new ProductService(repository, stamp);
            var inventory = new InventoryService(repository, stamp);

            await products.CreateAsync("STAFF-1", "Staff product", 19.99m, 6);
            await inventory.AdjustAsync("STAFF-1", 4, "Restock", "staff-demo-01");
            await products.UpdateDetailsAsync("STAFF-1", "Staff product revised", 21.50m);
            await products.ArchiveAsync("STAFF-1");
            await products.UnarchiveAsync("STAFF-1");
        }

        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(verificationContext);

            var product = await repository.FindProductBySkuAsync("STAFF-1");
            Assert.NotNull(product);
            Assert.Equal("Staff product revised", product.Description);
            Assert.Equal(21.50m, product.Price);
            Assert.Equal(10, product.StockQuantity);
            Assert.False(product.IsArchived);

            var history = await repository.GetAdjustmentsAsync(product.Id);
            var adjustment = Assert.Single(history);
            Assert.Equal(4, adjustment.QuantityChange);
            Assert.Equal(10, adjustment.ResultingQuantity);
            Assert.Equal("Restock", adjustment.Reason);
            Assert.Equal("staff-demo-01", adjustment.ActorIdentifier);

            var customerView = await repository.GetProductsAsync(includeArchived: false);
            Assert.Contains(customerView, item => item.Sku == "STAFF-1");

            var staffView = await repository.GetProductsAsync(includeArchived: true);
            Assert.Contains(staffView, item => item.Sku == "STAFF-1");
        }
    }

    [Fact]
    public async Task ArchivedProduct_IsHiddenFromCustomerView()
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

        var stamp = new TestTimeProvider(Now);

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(commandContext);
            await new ProductService(repository, stamp).ArchiveAsync("AUR-100");
        }

        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(verificationContext);

            var customerView = await repository.GetProductsAsync(includeArchived: false);
            Assert.DoesNotContain(customerView, item => item.Sku == "AUR-100");

            var staffView = await repository.GetProductsAsync(includeArchived: true);
            Assert.Contains(staffView, item => item.Sku == "AUR-100");
        }
    }

    [Fact]
    public async Task OrderPlacement_RecordsInventoryAdjustment()
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

        var stamp = new TestTimeProvider(Now);

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(commandContext);
            await new OrderService(repository, stamp)
                .PlaceAsync("customer-demo-001", [new OrderItemRequest("AUR-100", 2)]);
        }

        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(verificationContext);
            var product = await repository.FindProductBySkuAsync("AUR-100");
            Assert.NotNull(product);

            var history = await repository.GetAdjustmentsAsync(product.Id);
            var adjustment = Assert.Single(history);
            Assert.Equal(-2, adjustment.QuantityChange);
            Assert.Equal(16, adjustment.ResultingQuantity);
            Assert.Equal("Simulated order", adjustment.Reason);
        }
    }
}
