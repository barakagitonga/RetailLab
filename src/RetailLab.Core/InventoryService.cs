namespace RetailLab.Core;

public sealed class InventoryService(IRetailRepository repository, TimeProvider timeProvider)
{
    public async Task<InventoryAdjustment> AdjustAsync(
        string sku,
        int quantityChange,
        string reason,
        string actorIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        if (product.IsArchived)
        {
            throw new BusinessRuleException(
                $"Product {product.Sku} is archived. Unarchive it before adjusting stock.");
        }

        if (quantityChange == 0)
        {
            throw new BusinessRuleException("Stock adjustment must not be zero.");
        }

        var createdAtUtc = timeProvider.GetUtcNow();

        // Build the audit record before mutating so invalid reasons or actors
        // fail without changing stock.
        var adjustment = new InventoryAdjustment(
            product,
            quantityChange,
            product.StockQuantity + quantityChange,
            reason,
            actorIdentifier,
            createdAtUtc);

        product.AdjustStock(quantityChange);

        repository.AddInventoryAdjustment(adjustment);
        await repository.SaveProductChangesAsync(cancellationToken);

        return adjustment;
    }

    public async Task<IReadOnlyList<InventoryAdjustment>> GetHistoryAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        return await repository.GetAdjustmentsAsync(product.Id, cancellationToken);
    }
}
