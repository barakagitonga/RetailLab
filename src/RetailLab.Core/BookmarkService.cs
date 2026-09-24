namespace RetailLab.Core;

public sealed class BookmarkService(IRetailRepository repository, TimeProvider timeProvider)
{
    public async Task AddAsync(
        string customerIdentifier,
        string sku,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        var existing = await repository.FindBookmarkAsync(
            normalizedCustomerIdentifier,
            product.Id,
            cancellationToken);

        if (existing is not null)
        {
            throw new BusinessRuleException($"Product {product.Sku} is already bookmarked.");
        }

        repository.AddBookmark(
            new Bookmark(normalizedCustomerIdentifier, product, timeProvider.GetUtcNow()));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        string customerIdentifier,
        string sku,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        var bookmark = await repository.FindBookmarkAsync(
            normalizedCustomerIdentifier,
            product.Id,
            cancellationToken);

        if (bookmark is null)
        {
            throw new BusinessRuleException($"Product {product.Sku} is not bookmarked.");
        }

        repository.RemoveBookmark(bookmark);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
