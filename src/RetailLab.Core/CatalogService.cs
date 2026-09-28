namespace RetailLab.Core;

/// <summary>
/// Customer-visible catalogue lookups. Owns the "archived means invisible"
/// invariant so every presentation (console, web, future clients) shares one rule.
/// Knows nothing about prices display or locale; that belongs to the edge.
/// </summary>
public sealed class CatalogService(IRetailRepository repository)
{
    public Task<IReadOnlyList<Product>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default)
    {
        return repository.GetProductsAsync(includeArchived: false, cancellationToken);
    }

    /// <summary>
    /// Finds one active product by SKU (case-insensitive). Returns null when the
    /// SKU is missing or the product is archived, so callers show 404 for both.
    /// </summary>
    public async Task<Product?> FindActiveProductAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null || product.IsArchived)
        {
            return null;
        }

        return product;
    }
}
