using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class CheckoutServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid AttemptId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task StageAsync_StagesWithoutSaving()
    {
        var product = new Product("SKU-1", "Product", 12.50m, 5);
        var repository = new InMemoryRetailRepository(product);
        var orders = new OrderService(repository, new TestTimeProvider(Now));

        var staged = await orders.StageAsync(
            "customer-1",
            [new OrderItemRequest("SKU-1", 2)]);

        Assert.Equal(0, repository.SaveCount);
        Assert.Contains(staged, repository.Orders);
        Assert.Equal(3, product.StockQuantity);
        Assert.Single(repository.Adjustments);
    }

    [Fact]
    public async Task CheckoutBasketAsync_CreatesOrderReducesStockAuditsAndClearsBasket()
    {
        var first = new Product("SKU-1", "First product", 12.50m, 5);
        var second = new Product("SKU-2", "Second product", 7.00m, 10);
        var repository = new InMemoryRetailRepository(first, second);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 2);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-2", 1);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var savesBeforeCheckout = repository.SaveCount;

        var order = await checkout.CheckoutBasketAsync("customer-1", AttemptId);

        Assert.Equal(AttemptId, order.Id);
        Assert.Equal("customer-1", order.CustomerIdentifier);
        Assert.Equal(Now, order.PlacedAtUtc);
        Assert.Equal(2, order.Lines.Count);
        var firstLine = order.Lines.Single(line => line.Sku == "SKU-1");
        Assert.Equal("First product", firstLine.ProductDescription);
        Assert.Equal(12.50m, firstLine.UnitPrice);
        Assert.Equal(2, firstLine.Quantity);
        Assert.Equal(32.00m, order.Total);
        Assert.Equal(3, first.StockQuantity);
        Assert.Equal(9, second.StockQuantity);

        Assert.Equal(2, repository.Adjustments.Count);
        var firstAdjustment = repository.Adjustments.Single(adjustment => adjustment.ProductId == first.Id);
        Assert.Equal(-2, firstAdjustment.QuantityChange);
        Assert.Equal(3, firstAdjustment.ResultingQuantity);
        Assert.Equal("Simulated order", firstAdjustment.Reason);
        Assert.Equal("customer-1", firstAdjustment.ActorIdentifier);
        Assert.Equal(Now, firstAdjustment.CreatedAtUtc);

        Assert.Empty(repository.BasketItems);
        Assert.Single(repository.Orders);

        // The whole checkout (order, stock, audit records, basket removal)
        // commits in exactly one save.
        Assert.Equal(savesBeforeCheckout + 1, repository.SaveCount);
    }

    [Fact]
    public async Task CheckoutBasketAsync_SnapshotsCurrentPrice()
    {
        var product = new Product("SKU-1", "Product", 10.00m, 5);
        var repository = new InMemoryRetailRepository(product);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 1);

        // The price moves after the product entered the basket; the order must
        // record what the product costs at checkout time.
        product.UpdateDetails("Product", 14.00m);

        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var order = await checkout.CheckoutBasketAsync("customer-1", AttemptId);

        var line = Assert.Single(order.Lines);
        Assert.Equal(14.00m, line.UnitPrice);
        Assert.Equal(14.00m, order.Total);
    }

    [Fact]
    public async Task CheckoutBasketAsync_RejectsInsufficientStockWithoutChangingAnything()
    {
        var available = new Product("SKU-1", "Available product", 5m, 10);
        var insufficient = new Product("SKU-2", "Low-stock product", 7m, 1);
        var repository = new InMemoryRetailRepository(available, insufficient);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 2);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-2", 2);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var savesBeforeCheckout = repository.SaveCount;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => checkout.CheckoutBasketAsync("customer-1", AttemptId));

        Assert.Equal(10, available.StockQuantity);
        Assert.Equal(1, insufficient.StockQuantity);
        Assert.Empty(repository.Orders);
        Assert.Empty(repository.Adjustments);
        Assert.Equal(2, repository.BasketItems.Count);
        Assert.Equal(savesBeforeCheckout, repository.SaveCount);
    }

    [Fact]
    public async Task CheckoutBasketAsync_RejectsArchivedProductAndKeepsBasket()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 2);
        product.Archive(Now);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => checkout.CheckoutBasketAsync("customer-1", AttemptId));

        Assert.Empty(repository.Orders);
        Assert.Single(repository.BasketItems);
    }

    [Fact]
    public async Task CheckoutBasketAsync_RejectsEmptyBasket()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 10));
        var stamp = new TestTimeProvider(Now);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => checkout.CheckoutBasketAsync("customer-1", AttemptId));

        Assert.Empty(repository.Orders);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CheckoutBasketAsync_RejectsEmptyAttemptId()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 10));
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 1);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var savesBeforeCheckout = repository.SaveCount;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => checkout.CheckoutBasketAsync("customer-1", Guid.Empty));

        Assert.Empty(repository.Orders);
        Assert.Equal(savesBeforeCheckout, repository.SaveCount);
    }

    [Fact]
    public async Task CheckoutBasketAsync_ClearsOnlyCheckingOutCustomersBasket()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var stamp = new TestTimeProvider(Now);
        var basket = new BasketService(repository, stamp);
        await basket.AddAsync("customer-a", "SKU-1", 1);
        await basket.AddAsync("customer-b", "SKU-1", 3);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));

        await checkout.CheckoutBasketAsync("customer-a", AttemptId);

        Assert.Empty(await basket.GetBasketItemsAsync("customer-a"));
        var survivors = Assert.Single(await basket.GetBasketItemsAsync("customer-b"));
        Assert.Equal(3, survivors.Quantity);
    }

    [Fact]
    public async Task CheckoutBasketAsync_ReturnsSameOrderForDuplicateAttempt()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-1", "SKU-1", 2);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var savesBeforeCheckout = repository.SaveCount;

        var first = await checkout.CheckoutBasketAsync("customer-1", AttemptId);
        var second = await checkout.CheckoutBasketAsync("customer-1", AttemptId);

        Assert.Same(first, second);
        Assert.Single(repository.Orders);
        Assert.Equal(8, product.StockQuantity);
        Assert.Single(repository.Adjustments);
        Assert.Equal(savesBeforeCheckout + 1, repository.SaveCount);
    }

    [Fact]
    public async Task GetOrderAsync_IsolatesCustomers()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(repository, stamp).AddAsync("customer-a", "SKU-1", 1);
        var checkout = new CheckoutService(repository, new OrderService(repository, stamp));
        var order = await checkout.CheckoutBasketAsync("customer-a", AttemptId);

        Assert.NotNull(await checkout.GetOrderAsync("customer-a", order.Id));

        // A forged id for another customer's order reads as missing.
        Assert.Null(await checkout.GetOrderAsync("customer-b", order.Id));
        Assert.Null(await checkout.GetOrderAsync("customer-a", Guid.NewGuid()));

        Assert.Single(await checkout.GetOrdersAsync("customer-a"));
        Assert.Empty(await checkout.GetOrdersAsync("customer-b"));
    }

    [Fact]
    public async Task CheckoutBasketAsync_ReturnsWinnerAfterSaveConflict()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 10));
        var flaky = new FlakyCheckoutRepository(repository, keepStagedOrder: true);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(flaky, stamp).AddAsync("customer-1", "SKU-1", 2);
        var checkout = new CheckoutService(flaky, new OrderService(flaky, stamp));

        // The save reports a conflict, but the recovery lookup finds the
        // duplicate submission's order: idempotent success.
        var order = await checkout.CheckoutBasketAsync("customer-1", AttemptId);

        Assert.Equal(AttemptId, order.Id);
        Assert.Same(Assert.Single(repository.Orders), order);
    }

    [Fact]
    public async Task CheckoutBasketAsync_RethrowsConflictWhenNoWinnerExists()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 10));
        var flaky = new FlakyCheckoutRepository(repository, keepStagedOrder: false);
        var stamp = new TestTimeProvider(Now);
        await new BasketService(flaky, stamp).AddAsync("customer-1", "SKU-1", 2);
        var checkout = new CheckoutService(flaky, new OrderService(flaky, stamp));

        // The save reports a conflict and no order exists for the attempt id:
        // a genuine conflict the customer must retry.
        await Assert.ThrowsAsync<CheckoutConflictException>(
            () => checkout.CheckoutBasketAsync("customer-1", AttemptId));
    }

    /// <summary>
    /// Drives the catch-and-recheck branch deterministically: the first
    /// checkout save throws, then the recovery lookup either finds the staged
    /// order (a duplicate won) or misses (a genuine conflict).
    /// </summary>
    private sealed class FlakyCheckoutRepository(InMemoryRetailRepository inner, bool keepStagedOrder)
        : IRetailRepository
    {
        private bool _failed;

        public Task<IReadOnlyList<Product>> GetProductsAsync(
            bool includeArchived = false,
            CancellationToken cancellationToken = default) =>
            inner.GetProductsAsync(includeArchived, cancellationToken);

        public Task<Product?> FindProductBySkuAsync(
            string sku,
            CancellationToken cancellationToken = default) =>
            inner.FindProductBySkuAsync(sku, cancellationToken);

        public void AddProduct(Product product) => inner.AddProduct(product);

        public void AddInventoryAdjustment(InventoryAdjustment adjustment) =>
            inner.AddInventoryAdjustment(adjustment);

        public Task<IReadOnlyList<InventoryAdjustment>> GetAdjustmentsAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            inner.GetAdjustmentsAsync(productId, cancellationToken);

        public Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
            string customerIdentifier,
            CancellationToken cancellationToken = default) =>
            inner.GetBookmarksAsync(customerIdentifier, cancellationToken);

        public Task<Bookmark?> FindBookmarkAsync(
            string customerIdentifier,
            Guid productId,
            CancellationToken cancellationToken = default) =>
            inner.FindBookmarkAsync(customerIdentifier, productId, cancellationToken);

        public void AddBookmark(Bookmark bookmark) => inner.AddBookmark(bookmark);

        public void RemoveBookmark(Bookmark bookmark) => inner.RemoveBookmark(bookmark);

        public Task<IReadOnlyList<BasketItem>> GetBasketItemsAsync(
            string customerIdentifier,
            CancellationToken cancellationToken = default) =>
            inner.GetBasketItemsAsync(customerIdentifier, cancellationToken);

        public Task<BasketItem?> FindBasketItemAsync(
            string customerIdentifier,
            Guid productId,
            CancellationToken cancellationToken = default) =>
            inner.FindBasketItemAsync(customerIdentifier, productId, cancellationToken);

        public void AddBasketItem(BasketItem item) => inner.AddBasketItem(item);

        public void RemoveBasketItem(BasketItem item) => inner.RemoveBasketItem(item);

        public Task<IReadOnlyList<BasketItem>> GetBasketItemsForCheckoutAsync(
            string customerIdentifier,
            CancellationToken cancellationToken = default) =>
            inner.GetBasketItemsForCheckoutAsync(customerIdentifier, cancellationToken);

        public Task SaveBasketChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveBasketChangesAsync(cancellationToken);

        public void AddOrder(Order order) => inner.AddOrder(order);

        public Task<IReadOnlyList<Order>> GetOrdersAsync(
            string customerIdentifier,
            CancellationToken cancellationToken = default) =>
            inner.GetOrdersAsync(customerIdentifier, cancellationToken);

        public Task<Order?> FindOrderAsync(
            string customerIdentifier,
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            inner.FindOrderAsync(customerIdentifier, orderId, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);

        public Task SaveProductChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveProductChangesAsync(cancellationToken);

        public Task SaveCheckoutChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                if (!keepStagedOrder)
                {
                    inner.Orders.Clear();
                }

                throw new CheckoutConflictException();
            }

            return inner.SaveCheckoutChangesAsync(cancellationToken);
        }
    }
}
