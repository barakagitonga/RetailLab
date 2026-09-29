namespace RetailLab.Core;

/// <summary>
/// Per-customer shopping basket. Owns every basket rule — unknown and
/// archived products, quantities, overflow — so PageModels only bind input,
/// call one method, and map <see cref="BusinessRuleException" /> and
/// <see cref="BasketConflictException" /> to notices. The basket never
/// reserves inventory: stock is checked when an order is placed (Tutorial 5B),
/// so out-of-stock products can be added and retained.
/// </summary>
public sealed class BasketService(IRetailRepository repository, TimeProvider timeProvider)
{
    /// <summary>
    /// Lists the customer's basket with products included. Archived products
    /// stay visible (the repository applies no archived filter); only removal
    /// is offered for them.
    /// </summary>
    public Task<IReadOnlyList<BasketItem>> GetBasketItemsAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        return repository.GetBasketItemsAsync(normalizedCustomerIdentifier, cancellationToken);
    }

    public async Task<BasketItem> AddAsync(
        string customerIdentifier,
        string sku,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        var product = await FindProductOrThrowAsync(sku, cancellationToken);

        if (product.IsArchived)
        {
            throw new BusinessRuleException("That product is no longer available.");
        }

        if (quantity <= 0)
        {
            throw new BusinessRuleException("Quantity must be at least 1.");
        }

        var existing = await repository.FindBasketItemAsync(
            normalizedCustomerIdentifier,
            product.Id,
            cancellationToken);

        BasketItem item;
        if (existing is null)
        {
            item = new BasketItem(
                normalizedCustomerIdentifier,
                product,
                quantity,
                timeProvider.GetUtcNow());
            repository.AddBasketItem(item);
        }
        else
        {
            try
            {
                var merged = checked(existing.Quantity + quantity);
                existing.SetQuantity(merged, timeProvider.GetUtcNow());
            }
            catch (OverflowException)
            {
                throw new BusinessRuleException("That quantity is too large. Please choose a smaller quantity.");
            }

            item = existing;
        }

        await repository.SaveBasketChangesAsync(cancellationToken);
        return item;
    }

    public async Task<BasketItem> UpdateAsync(
        string customerIdentifier,
        string sku,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        var product = await FindProductOrThrowAsync(sku, cancellationToken);

        if (product.IsArchived)
        {
            throw new BusinessRuleException("That product is no longer available.");
        }

        if (quantity <= 0)
        {
            throw new BusinessRuleException(
                "Quantity must be at least 1. To remove this product from your basket, use Remove.");
        }

        var item = await repository.FindBasketItemAsync(
            normalizedCustomerIdentifier,
            product.Id,
            cancellationToken);

        if (item is null)
        {
            throw new BusinessRuleException("That product is not in your basket.");
        }

        item.SetQuantity(quantity, timeProvider.GetUtcNow());
        await repository.SaveBasketChangesAsync(cancellationToken);
        return item;
    }

    public async Task<BasketItem> RemoveAsync(
        string customerIdentifier,
        string sku,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);

        // Removal deliberately uses the all-products lookup (never the
        // active-only catalogue): archived products left in the basket must
        // still be removable.
        Product? product = null;
        if (!string.IsNullOrWhiteSpace(sku))
        {
            product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        }

        if (product is null)
        {
            throw new BusinessRuleException("That product is not in your basket.");
        }

        var item = await repository.FindBasketItemAsync(
            normalizedCustomerIdentifier,
            product.Id,
            cancellationToken);

        if (item is null)
        {
            throw new BusinessRuleException("That product is not in your basket.");
        }

        repository.RemoveBasketItem(item);
        await repository.SaveBasketChangesAsync(cancellationToken);
        return item;
    }

    private async Task<Product> FindProductOrThrowAsync(string sku, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new BusinessRuleException("Please choose a product first.");
        }

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException("That product is no longer available.");
        }

        return product;
    }
}
