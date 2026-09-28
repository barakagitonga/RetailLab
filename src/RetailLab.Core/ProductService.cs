namespace RetailLab.Core;

public sealed class ProductService(IRetailRepository repository, TimeProvider timeProvider)
{
    public async Task<Product> CreateAsync(
        string sku,
        string description,
        decimal price,
        int stockQuantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var existing = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException($"A product with SKU '{sku.Trim()}' already exists.");
        }

        var product = new Product(sku, description, price, stockQuantity);
        repository.AddProduct(product);
        await repository.SaveChangesAsync(cancellationToken);

        return product;
    }

    public async Task<Product> UpdateDetailsAsync(
        string sku,
        string description,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var product = await FindOrThrowAsync(sku, cancellationToken);

        product.UpdateDetails(description, price);
        await repository.SaveChangesAsync(cancellationToken);

        return product;
    }

    public async Task<Product> ArchiveAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        var product = await FindOrThrowAsync(sku, cancellationToken);

        product.Archive(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return product;
    }

    public async Task<Product> UnarchiveAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        var product = await FindOrThrowAsync(sku, cancellationToken);

        product.Unarchive();
        await repository.SaveChangesAsync(cancellationToken);

        return product;
    }

    private async Task<Product> FindOrThrowAsync(string sku, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        return product;
    }
}
