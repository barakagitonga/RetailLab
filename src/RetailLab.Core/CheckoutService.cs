namespace RetailLab.Core;

/// <summary>
/// Web basket checkout. Reuses <see cref="OrderService" /> staging so web and
/// console ordering share one validation and stock/audit implementation; the
/// basket removal joins the same atomic save. The checkout-attempt identifier
/// doubles as the new order's id, so a repeated submission finds the original
/// order instead of creating another one.
/// </summary>
public sealed class CheckoutService(IRetailRepository repository, OrderService orders)
{
    public async Task<Order> CheckoutBasketAsync(
        string customerIdentifier,
        Guid checkoutAttemptId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);

        if (checkoutAttemptId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Your checkout expired. Please review your basket and try again.");
        }

        // Idempotent replay: a repeated submission with the same attempt id
        // returns the original order. The lookup is customer-scoped, so a
        // forged id can never resolve to another customer's order.
        var existing = await repository.FindOrderAsync(
            normalizedCustomerIdentifier,
            checkoutAttemptId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var basketItems = await repository.GetBasketItemsForCheckoutAsync(
            normalizedCustomerIdentifier,
            cancellationToken);
        if (basketItems.Count == 0)
        {
            throw new BusinessRuleException("Your basket is empty.");
        }

        var requests = basketItems
            .Select(item => new OrderItemRequest(item.Product.Sku, item.Quantity))
            .ToList();

        var order = await orders.StageAsync(
            normalizedCustomerIdentifier,
            requests,
            checkoutAttemptId,
            cancellationToken);

        foreach (var item in basketItems)
        {
            repository.RemoveBasketItem(item);
        }

        try
        {
            await repository.SaveCheckoutChangesAsync(cancellationToken);
        }
        catch (CheckoutConflictException)
        {
            // A duplicate submission may have won the race to insert this
            // order id. Re-read committed state: a winner means idempotent
            // success, while a miss means a genuine conflict to retry.
            var winner = await repository.FindOrderAsync(
                normalizedCustomerIdentifier,
                checkoutAttemptId,
                cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }

        return order;
    }

    /// <summary>
    /// Finds one order for confirmation display. Returns null when the id is
    /// unknown or belongs to another customer; callers show 404 for both.
    /// </summary>
    public Task<Order?> GetOrderAsync(
        string customerIdentifier,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        return repository.FindOrderAsync(normalizedCustomerIdentifier, orderId, cancellationToken);
    }

    public Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedCustomerIdentifier = CustomerIdentifier.Normalize(customerIdentifier);
        return repository.GetOrdersAsync(normalizedCustomerIdentifier, cancellationToken);
    }
}
