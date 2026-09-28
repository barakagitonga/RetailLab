namespace RetailLab.Core;

public sealed class BookmarkService(IRetailRepository repository, TimeProvider timeProvider)
{
    /// <summary>
    /// Lists the customer's visible bookmarks (archived products are excluded
    /// by the repository). Presentation layers derive favourite sets from this.
    /// </summary>
    public Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        return repository.GetBookmarksAsync(normalizedCustomerIdentifier, cancellationToken);
    }

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

        if (product.IsArchived)
        {
            throw new BusinessRuleException($"Product {product.Sku} is archived and cannot be bookmarked.");
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
