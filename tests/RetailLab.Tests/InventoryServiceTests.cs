using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class InventoryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AdjustAsync_AppliesDeltaAndRecordsAudit()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        var adjustment = await service.AdjustAsync("SKU-1", -3, "Damaged items", "staff-demo-01");

        Assert.Equal(7, product.StockQuantity);
        Assert.Equal(-3, adjustment.QuantityChange);
        Assert.Equal(7, adjustment.ResultingQuantity);
        Assert.Equal("Damaged items", adjustment.Reason);
        Assert.Equal("staff-demo-01", adjustment.ActorIdentifier);
        Assert.Equal(Now, adjustment.CreatedAtUtc);
        Assert.Single(repository.Adjustments);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AdjustAsync_AcceptsPositiveDelta()
    {
        var product = new Product("SKU-1", "Product", 5m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await service.AdjustAsync("sku-1", 5, "Restock", "staff-demo-01");

        Assert.Equal(7, product.StockQuantity);
    }

    [Fact]
    public async Task AdjustAsync_RejectsZeroDeltaWithoutChangingStock()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AdjustAsync("SKU-1", 0, "No change", "staff-demo-01"));

        Assert.Equal(10, product.StockQuantity);
        Assert.Empty(repository.Adjustments);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AdjustAsync_RejectsNegativeResultWithoutChangingStock()
    {
        var product = new Product("SKU-1", "Product", 5m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AdjustAsync("SKU-1", -3, "Overdraw", "staff-demo-01"));

        Assert.Equal(2, product.StockQuantity);
        Assert.Empty(repository.Adjustments);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AdjustAsync_RejectsBlankReasonWithoutChangingStock()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AdjustAsync("SKU-1", 2, "   ", "staff-demo-01"));

        Assert.Equal(10, product.StockQuantity);
        Assert.Empty(repository.Adjustments);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AdjustAsync_RejectsArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        product.Archive(Now);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AdjustAsync("SKU-1", 2, "Restock", "staff-demo-01"));

        Assert.Equal(10, product.StockQuantity);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AdjustAsync_RejectsUnknownSku()
    {
        var repository = new InMemoryRetailRepository();
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AdjustAsync("MISSING", 2, "Restock", "staff-demo-01"));

        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsAdjustmentsForProduct()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var service = new InventoryService(repository, new TestTimeProvider(Now));

        await service.AdjustAsync("SKU-1", 2, "Restock", "staff-demo-01");
        await service.AdjustAsync("SKU-1", -1, "Damaged", "staff-demo-01");

        var history = await service.GetHistoryAsync("SKU-1");

        Assert.Equal(2, history.Count);
    }
}
