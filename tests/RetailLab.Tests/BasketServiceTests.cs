using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class BasketServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_AddsNewItemWithQuantityAndTimestamps()
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        var item = await service.AddAsync("customer-1", "sku-1", 2);

        Assert.Equal("customer-1", item.CustomerIdentifier);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(Now, item.CreatedAtUtc);
        Assert.Equal(Now, item.UpdatedAtUtc);
        Assert.Same(product, item.Product);
        Assert.Single(repository.BasketItems);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AddAsync_IncreasesExistingQuantity()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await service.AddAsync("customer-1", "SKU-1", 1);
        var item = await service.AddAsync("customer-1", "SKU-1", 2);

        Assert.Equal(3, item.Quantity);
        Assert.Single(repository.BasketItems);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task AddAsync_AllowsOutOfStockProduct()
    {
        // The basket reserves nothing; checkout (Tutorial 5B) enforces stock.
        var product = new Product("SKU-1", "Product", 10m, 0);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        var item = await service.AddAsync("customer-1", "SKU-1", 1);

        Assert.Equal(1, item.Quantity);
        Assert.Single(repository.BasketItems);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("MISSING")]
    public async Task AddAsync_RejectsBlankAndUnknownSku(string sku)
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync("customer-1", sku, 1));

        Assert.Empty(repository.BasketItems);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AddAsync_RejectsArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        product.Archive(Now);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync("customer-1", "SKU-1", 1));

        Assert.Empty(repository.BasketItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddAsync_RejectsNonPositiveQuantityAsBusinessRule(int quantity)
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        // Must be BusinessRuleException (customer-correctable), never the
        // entity's ArgumentOutOfRangeException, so PageModels catch one type.
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync("customer-1", "SKU-1", quantity));
    }

    [Fact]
    public async Task AddAsync_TranslatesOverflowIntoBusinessRule()
    {
        var product = new Product("SKU-1", "Product", 10m, 5);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", int.MaxValue);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync("customer-1", "SKU-1", 1));

        Assert.Contains("too large", exception.Message);
        Assert.Equal(int.MaxValue, Assert.Single(repository.BasketItems).Quantity);
    }

    [Fact]
    public async Task UpdateAsync_SetsQuantity()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 1);

        var item = await service.UpdateAsync("customer-1", "SKU-1", 4);

        Assert.Equal(4, item.Quantity);
        Assert.Equal(2, repository.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task UpdateAsync_RejectsNonPositiveQuantityAndKeepsRemovalSeparate(int quantity)
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 2);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync("customer-1", "SKU-1", quantity));

        Assert.Contains("Remove", exception.Message);
        Assert.Equal(2, Assert.Single(repository.BasketItems).Quantity);
    }

    [Fact]
    public async Task UpdateAsync_RejectsProductNotInBasket()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync("customer-1", "SKU-1", 2));
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnknownSku()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync("customer-1", "MISSING", 2));
    }

    [Fact]
    public async Task UpdateAsync_RejectsArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 1);
        product.Archive(Now);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync("customer-1", "SKU-1", 2));

        Assert.Equal(1, Assert.Single(repository.BasketItems).Quantity);
    }

    [Fact]
    public async Task RemoveAsync_RemovesExistingItem()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 2);

        var removed = await service.RemoveAsync("customer-1", "SKU-1");

        Assert.Equal(product.Id, removed.ProductId);
        Assert.Empty(repository.BasketItems);
        Assert.Equal(2, repository.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_WorksForArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 2);
        product.Archive(Now);

        await service.RemoveAsync("customer-1", "SKU-1");

        Assert.Empty(repository.BasketItems);
    }

    [Theory]
    [InlineData("MISSING")]
    [InlineData("SKU-1")]
    public async Task RemoveAsync_RejectsUnknownSkuAndMissingItem(string sku)
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RemoveAsync("customer-1", sku));
    }

    [Fact]
    public async Task GetBasketItemsAsync_IsolatesTwoCustomersSharingOneProduct()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));

        await service.AddAsync("customer-a", "SKU-1", 1);
        await service.AddAsync("customer-b", "SKU-1", 3);

        Assert.Equal(1, Assert.Single(await service.GetBasketItemsAsync("customer-a")).Quantity);
        Assert.Equal(3, Assert.Single(await service.GetBasketItemsAsync("customer-b")).Quantity);

        await service.RemoveAsync("customer-a", "SKU-1");

        Assert.Empty(await service.GetBasketItemsAsync("customer-a"));
        Assert.Single(await service.GetBasketItemsAsync("customer-b"));
    }

    [Fact]
    public async Task GetBasketItemsAsync_RetainsArchivedProducts()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 2);

        product.Archive(Now);

        var item = Assert.Single(await service.GetBasketItemsAsync("customer-1"));
        Assert.Equal(2, item.Quantity);
        Assert.True(item.Product.IsArchived);
    }

    [Fact]
    public async Task GetBasketItemsAsync_NormalizesCustomerIdentifier()
    {
        var product = new Product("SKU-1", "Product", 10m, 20);
        var repository = new InMemoryRetailRepository(product);
        var service = new BasketService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1", 1);

        Assert.Single(await service.GetBasketItemsAsync("  customer-1  "));
    }
}
