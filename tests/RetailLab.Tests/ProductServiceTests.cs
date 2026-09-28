using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class ProductServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_AddsNewProduct()
    {
        var repository = new InMemoryRetailRepository();
        var service = new ProductService(repository, new TestTimeProvider(Now));

        var product = await service.CreateAsync("NEW-1", "New product", 9.99m, 4);

        Assert.Equal("NEW-1", product.Sku);
        Assert.Single(repository.Products);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSkuWithoutSaving()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 2));
        var service = new ProductService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync("sku-1", "Duplicate", 5m, 2));

        Assert.Single(repository.Products);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateDetailsAsync_ChangesDescriptionAndPrice()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Old", 5m, 2));
        var service = new ProductService(repository, new TestTimeProvider(Now));

        var product = await service.UpdateDetailsAsync("SKU-1", "New", 7.50m);

        Assert.Equal("New", product.Description);
        Assert.Equal(7.50m, product.Price);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateDetailsAsync_RejectsArchivedProductWithoutSaving()
    {
        var product = new Product("SKU-1", "Product", 5m, 2);
        product.Archive(Now);
        var repository = new InMemoryRetailRepository(product);
        var service = new ProductService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateDetailsAsync("SKU-1", "Changed", 6m));

        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ArchiveAsync_MarksProductArchived()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 2));
        var service = new ProductService(repository, new TestTimeProvider(Now));

        var product = await service.ArchiveAsync("SKU-1");

        Assert.True(product.IsArchived);
        Assert.Equal(Now, product.ArchivedAtUtc);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task UnarchiveAsync_RestoresArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 5m, 2);
        product.Archive(Now);
        var repository = new InMemoryRetailRepository(product);
        var service = new ProductService(repository, new TestTimeProvider(Now));

        await service.UnarchiveAsync("SKU-1");

        Assert.False(product.IsArchived);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ArchiveAsync_RejectsUnknownSku()
    {
        var repository = new InMemoryRetailRepository();
        var service = new ProductService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ArchiveAsync("MISSING"));

        Assert.Equal(0, repository.SaveCount);
    }
}
