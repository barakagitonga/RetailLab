namespace RetailLab.Core;

public sealed class OrderService(IRetailRepository repository, TimeProvider timeProvider)
{
    public async Task<Order> PlaceAsync(
        string customerIdentifier,
        IEnumerable<OrderItemRequest> requestedItems,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        ArgumentNullException.ThrowIfNull(requestedItems);

        var requests = requestedItems.ToList();
        if (requests.Count == 0)
        {
            throw new BusinessRuleException("An order must contain at least one item.");
        }

        if (requests.Any(request => string.IsNullOrWhiteSpace(request.Sku)))
        {
            throw new BusinessRuleException("Every order item must have a SKU.");
        }

        if (requests.Any(request => request.Quantity <= 0))
        {
            throw new BusinessRuleException("Every order quantity must be greater than zero.");
        }

        var combinedRequests = requests
            .GroupBy(request => request.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrderItemRequest(group.Key, group.Sum(item => item.Quantity)))
            .ToList();

        var productsAndQuantities = new List<(Product Product, int Quantity)>();

        foreach (var request in combinedRequests)
        {
            var product = await repository.FindProductBySkuAsync(request.Sku, cancellationToken);
            if (product is null)
            {
                throw new BusinessRuleException($"No product with SKU '{request.Sku}' was found.");
            }

            if (product.IsArchived)
            {
                throw new BusinessRuleException(
                    $"Product {product.Sku} is archived and cannot be ordered.");
            }

            if (request.Quantity > product.StockQuantity)
            {
                throw new BusinessRuleException(
                    $"Insufficient stock for {product.Sku}. Requested {request.Quantity}, " +
                    $"but only {product.StockQuantity} available.");
            }

            productsAndQuantities.Add((product, request.Quantity));
        }

        var order = new Order(normalizedCustomerIdentifier, timeProvider.GetUtcNow());

        foreach (var (product, quantity) in productsAndQuantities)
        {
            product.ReduceStock(quantity);
            order.AddLine(product, quantity);
            repository.AddInventoryAdjustment(
                new InventoryAdjustment(
                    product,
                    -quantity,
                    product.StockQuantity,
                    "Simulated order",
                    normalizedCustomerIdentifier,
                    order.PlacedAtUtc));
        }

        repository.AddOrder(order);
        await repository.SaveChangesAsync(cancellationToken);

        return order;
    }
}
