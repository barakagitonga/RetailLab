using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class CheckoutPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 11, 0, 0, TimeSpan.Zero);

    private static readonly Guid AttemptId = new("22222222-2222-2222-2222-222222222222");

    // SQLite extended result codes (https://www.sqlite.org/rescode.html).
    private const int SqliteConstraintPrimaryKey = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int SqliteConstraintCheck = 275; // SQLITE_CONSTRAINT_CHECK

    [Fact]
    public async Task Checkout_RoundTripsPrescribedOrderId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var basketContext = new RetailLabDbContext(options))
        {
            var basket = new BasketService(
                new EfRetailRepository(basketContext),
                new TestTimeProvider(Now));
            await basket.AddAsync("customer-1", "AUR-100", 2);
            await basket.AddAsync("customer-1", "BRI-210", 1);
        }

        await using (var checkoutContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(checkoutContext);
            var checkout = new CheckoutService(
                repository,
                new OrderService(repository, new TestTimeProvider(Now)));

            var order = await checkout.CheckoutBasketAsync("customer-1", AttemptId);

            Assert.Equal(AttemptId, order.Id);
            Assert.Equal(2, order.Lines.Count);
            Assert.Equal(66.40m, order.Total);
        }

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        var stored = await verificationRepository.FindOrderAsync("customer-1", AttemptId);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Lines.Count);

        var mug = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(mug);
        Assert.Equal(16, mug.StockQuantity);

        var tote = await verificationRepository.FindProductBySkuAsync("BRI-210");
        Assert.NotNull(tote);
        Assert.Equal(24, tote.StockQuantity);

        var history = await verificationRepository.GetAdjustmentsAsync(mug.Id);
        var adjustment = Assert.Single(history);
        Assert.Equal(-2, adjustment.QuantityChange);
        Assert.Equal(16, adjustment.ResultingQuantity);
        Assert.Equal("Simulated order", adjustment.Reason);
        Assert.Equal("customer-1", adjustment.ActorIdentifier);

        Assert.Empty(await verificationRepository.GetBasketItemsAsync("customer-1"));
    }

    [Fact]
    public async Task SameAttempt_PkCollisionLoserRecoversWinnerOnSameContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var basketContext = new RetailLabDbContext(options))
        {
            await new BasketService(new EfRetailRepository(basketContext), new TestTimeProvider(Now))
                .AddAsync("customer-1", "AUR-100", 2);
        }

        // The winning submission commits the order with the attempt id.
        await using (var winnerContext = new RetailLabDbContext(options))
        {
            var winnerRepository = new EfRetailRepository(winnerContext);
            await new CheckoutService(
                    winnerRepository,
                    new OrderService(winnerRepository, new TestTimeProvider(Now)))
                .CheckoutBasketAsync("customer-1", AttemptId);
        }

        // The losing submission stages the same attempt id afterwards: fresh
        // product state (so no version conflict), the same order id (so the
        // insert must collide on the primary key).
        await using var loserContext = new RetailLabDbContext(options);
        var loserRepository = new EfRetailRepository(loserContext);
        await new BasketService(loserRepository, new TestTimeProvider(Now))
            .AddAsync("customer-1", "AUR-100", 2);
        await new OrderService(loserRepository, new TestTimeProvider(Now))
            .StageAsync("customer-1", [new OrderItemRequest("AUR-100", 2)], AttemptId);
        foreach (var item in await loserRepository.GetBasketItemsForCheckoutAsync("customer-1"))
        {
            loserRepository.RemoveBasketItem(item);
        }

        var conflict = await Assert.ThrowsAsync<CheckoutConflictException>(
            () => loserRepository.SaveCheckoutChangesAsync());

        // The translated failure really is the duplicate order insert: a key
        // collision, not a concurrency loss.
        Assert.IsType<DbUpdateException>(conflict.InnerException);
        Assert.IsNotType<DbUpdateConcurrencyException>(conflict.InnerException);
        var sqlite = Assert.IsType<SqliteException>(conflict.InnerException.InnerException);
        Assert.Equal(SqliteConstraintPrimaryKey, sqlite.SqliteExtendedErrorCode);

        // Recovery on the same DbContext state: the rolled-back tracked
        // insert must not hide the winner's committed order.
        var recovered = await loserRepository.FindOrderAsync("customer-1", AttemptId);
        Assert.NotNull(recovered);
        Assert.Equal(AttemptId, recovered.Id);
        var recoveredLine = Assert.Single(recovered.Lines);
        Assert.Equal("AUR-100", recoveredLine.Sku);
        Assert.Equal(2, recoveredLine.Quantity);
        Assert.Equal(49.90m, recovered.Total);

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        // The loser rolled back entirely: stock kept the winner's value, one
        // order exists, and the re-added basket line survived its deletion.
        var mug = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(mug);
        Assert.Equal(16, mug.StockQuantity);
        Assert.Single(await verificationRepository.GetOrdersAsync("customer-1"));
        var basketLine = Assert.Single(await verificationRepository.GetBasketItemsAsync("customer-1"));
        Assert.Equal(2, basketLine.Quantity);
    }

    [Fact]
    public async Task TwoCustomers_FinalUnit_ExactlyOneOrderSucceeds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var seedContext = new RetailLabDbContext(options))
        {
            var seedRepository = new EfRetailRepository(seedContext);
            var stamp = new TestTimeProvider(Now);
            await new ProductService(seedRepository, stamp)
                .CreateAsync("LAST-1", "Last unit product", 9.99m, 1);
            var basket = new BasketService(seedRepository, stamp);
            await basket.AddAsync("customer-a", "LAST-1", 1);
            await basket.AddAsync("customer-b", "LAST-1", 1);
        }

        // Customer B loads first and goes stale while customer A checks out.
        await using var contextB = new RetailLabDbContext(options);
        var repositoryB = new EfRetailRepository(contextB);
        Assert.Single(await repositoryB.GetBasketItemsForCheckoutAsync("customer-b"));

        await using (var contextA = new RetailLabDbContext(options))
        {
            var repositoryA = new EfRetailRepository(contextA);
            await new CheckoutService(
                    repositoryA,
                    new OrderService(repositoryA, new TestTimeProvider(Now)))
                .CheckoutBasketAsync("customer-a", Guid.NewGuid());
        }

        var checkoutB = new CheckoutService(
            repositoryB,
            new OrderService(repositoryB, new TestTimeProvider(Now)));
        await Assert.ThrowsAsync<CheckoutConflictException>(
            () => checkoutB.CheckoutBasketAsync("customer-b", Guid.NewGuid()));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        var product = await verificationRepository.FindProductBySkuAsync("LAST-1");
        Assert.NotNull(product);
        Assert.Equal(0, product.StockQuantity);

        Assert.Single(await verificationRepository.GetOrdersAsync("customer-a"));
        Assert.Empty(await verificationRepository.GetOrdersAsync("customer-b"));
        Assert.Empty(await verificationRepository.GetBasketItemsAsync("customer-a"));
        var survivors = Assert.Single(await verificationRepository.GetBasketItemsAsync("customer-b"));
        Assert.Equal(1, survivors.Quantity);
    }

    [Fact]
    public async Task PriceChangeAfterLoad_StaleCheckoutRollsBack()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var seedContext = new RetailLabDbContext(options))
        {
            await new BasketService(new EfRetailRepository(seedContext), new TestTimeProvider(Now))
                .AddAsync("customer-1", "AUR-100", 1);
        }

        // The checkout loads, then a staff price change commits first.
        await using var checkoutContext = new RetailLabDbContext(options);
        var checkoutRepository = new EfRetailRepository(checkoutContext);
        Assert.Single(await checkoutRepository.GetBasketItemsForCheckoutAsync("customer-1"));

        await using (var staffContext = new RetailLabDbContext(options))
        {
            await new ProductService(new EfRetailRepository(staffContext), new TestTimeProvider(Now))
                .UpdateDetailsAsync("AUR-100", "Aurora insulated travel mug", 99.99m);
        }

        var checkout = new CheckoutService(
            checkoutRepository,
            new OrderService(checkoutRepository, new TestTimeProvider(Now)));
        await Assert.ThrowsAsync<CheckoutConflictException>(
            () => checkout.CheckoutBasketAsync("customer-1", Guid.NewGuid()));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        // Nothing was recorded at the stale price: no order exists at all.
        Assert.Empty(await verificationRepository.GetOrdersAsync("customer-1"));
        var mug = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(mug);
        Assert.Equal(99.99m, mug.Price);
        Assert.Equal(18, mug.StockQuantity);
        Assert.Empty(await verificationRepository.GetAdjustmentsAsync(mug.Id));
        var basketLine = Assert.Single(await verificationRepository.GetBasketItemsAsync("customer-1"));
        Assert.Equal(1, basketLine.Quantity);
    }

    [Fact]
    public async Task ArchiveAfterLoad_StaleCheckoutRollsBack()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var seedContext = new RetailLabDbContext(options))
        {
            await new BasketService(new EfRetailRepository(seedContext), new TestTimeProvider(Now))
                .AddAsync("customer-1", "AUR-100", 1);
        }

        // The checkout loads, then a staff archive commits first.
        await using var checkoutContext = new RetailLabDbContext(options);
        var checkoutRepository = new EfRetailRepository(checkoutContext);
        Assert.Single(await checkoutRepository.GetBasketItemsForCheckoutAsync("customer-1"));

        await using (var staffContext = new RetailLabDbContext(options))
        {
            await new ProductService(new EfRetailRepository(staffContext), new TestTimeProvider(Now))
                .ArchiveAsync("AUR-100");
        }

        var checkout = new CheckoutService(
            checkoutRepository,
            new OrderService(checkoutRepository, new TestTimeProvider(Now)));
        await Assert.ThrowsAsync<CheckoutConflictException>(
            () => checkout.CheckoutBasketAsync("customer-1", Guid.NewGuid()));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        Assert.Empty(await verificationRepository.GetOrdersAsync("customer-1"));
        var mug = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(mug);
        Assert.True(mug.IsArchived);
        Assert.Equal(18, mug.StockQuantity);
        var basketLine = Assert.Single(await verificationRepository.GetBasketItemsAsync("customer-1"));
        Assert.Equal(1, basketLine.Quantity);
        Assert.True(basketLine.Product.IsArchived);
    }

    [Fact]
    public async Task StaleBasketEdit_RollsBackStockAndOrderWithBasket()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using (var seedContext = new RetailLabDbContext(options))
        {
            await new BasketService(new EfRetailRepository(seedContext), new TestTimeProvider(Now))
                .AddAsync("customer-1", "AUR-100", 1);
        }

        // The checkout loads, then a basket quantity edit commits first.
        await using var checkoutContext = new RetailLabDbContext(options);
        var checkoutRepository = new EfRetailRepository(checkoutContext);
        Assert.Single(await checkoutRepository.GetBasketItemsForCheckoutAsync("customer-1"));

        await using (var editContext = new RetailLabDbContext(options))
        {
            await new BasketService(new EfRetailRepository(editContext), new TestTimeProvider(Now))
                .UpdateAsync("customer-1", "AUR-100", 5);
        }

        var checkout = new CheckoutService(
            checkoutRepository,
            new OrderService(checkoutRepository, new TestTimeProvider(Now)));
        await Assert.ThrowsAsync<CheckoutConflictException>(
            () => checkout.CheckoutBasketAsync("customer-1", Guid.NewGuid()));

        await using var verificationContext = new RetailLabDbContext(options);
        var verificationRepository = new EfRetailRepository(verificationContext);

        // The stale basket delete failed, so the stock change and the order
        // rolled back with it; the winning edit survived.
        Assert.Empty(await verificationRepository.GetOrdersAsync("customer-1"));
        var mug = await verificationRepository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(mug);
        Assert.Equal(18, mug.StockQuantity);
        Assert.Empty(await verificationRepository.GetAdjustmentsAsync(mug.Id));
        var basketLine = Assert.Single(await verificationRepository.GetBasketItemsAsync("customer-1"));
        Assert.Equal(5, basketLine.Quantity);
    }

    [Fact]
    public async Task UnexpectedCheckoutConstraintFailure_PropagatesAsDbUpdateException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var initializationContext = new RetailLabDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(initializationContext);
        }

        await using var context = new RetailLabDbContext(options);
        var repository = new EfRetailRepository(context);
        await new BasketService(repository, new TestTimeProvider(Now))
            .AddAsync("customer-1", "AUR-100", 1);

        // Bypass the entity guards the way only a bug could, so the database
        // check constraint (not a key collision) rejects the save.
        var product = await repository.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(product);
        var item = await repository.FindBasketItemAsync("customer-1", product.Id);
        Assert.NotNull(item);
        context.Entry(item).Property(basketItem => basketItem.Quantity).CurrentValue = 0;

        // An unexpected constraint failure must reach the normal
        // unexpected-error handling, never masquerade as a customer retry.
        var failure = await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.SaveCheckoutChangesAsync());
        Assert.IsType<DbUpdateException>(failure);
        var sqlite = Assert.IsType<SqliteException>(failure.InnerException);
        Assert.Equal(SqliteConstraintCheck, sqlite.SqliteExtendedErrorCode);
    }
}
