using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class BasketPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 13, 0, 0, TimeSpan.Zero);

    // SQLite extended result codes (https://www.sqlite.org/rescode.html).
    private const int SqliteConstraintPrimaryKey = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int SqliteConstraintCheck = 275; // SQLITE_CONSTRAINT_CHECK

    [Fact]
    public async Task BasketRoundTrip_PersistsItemsAcrossContexts()
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

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var service = new BasketService(
                new EfRetailRepository(commandContext),
                new TestTimeProvider(Now));
            await service.AddAsync("customer-1", "AUR-100", 2);
            await service.AddAsync("customer-1", "BRI-210", 1);
            await service.UpdateAsync("customer-1", "AUR-100", 3);
        }

        await using var verificationContext = new RetailLabDbContext(options);
        var service2 = new BasketService(
            new EfRetailRepository(verificationContext),
            new TestTimeProvider(Now));

        var items = await service2.GetBasketItemsAsync("customer-1");

        // Ordered by SKU with products included in the same query.
        Assert.Equal(["AUR-100", "BRI-210"], items.Select(item => item.Product.Sku));
        Assert.Equal(3, items.First(item => item.Product.Sku == "AUR-100").Quantity);
        Assert.Equal("Aurora insulated travel mug", items[0].Product.Description);

        await service2.RemoveAsync("customer-1", "BRI-210");
        Assert.Single(await service2.GetBasketItemsAsync("customer-1"));
    }

    [Fact]
    public async Task BasketItems_RetainArchivedProductsButOnlyRemovalWorks()
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

        await using (var commandContext = new RetailLabDbContext(options))
        {
            var repository = new EfRetailRepository(commandContext);
            await new BasketService(repository, new TestTimeProvider(Now))
                .AddAsync("customer-1", "AUR-100", 2);
            await new ProductService(repository, new TestTimeProvider(Now))
                .ArchiveAsync("AUR-100");
        }

        await using var verificationContext = new RetailLabDbContext(options);
        var service = new BasketService(
            new EfRetailRepository(verificationContext),
            new TestTimeProvider(Now));

        // Retained and visible despite the archive.
        var item = Assert.Single(await service.GetBasketItemsAsync("customer-1"));
        Assert.True(item.Product.IsArchived);

        // Quantity edits are rejected for archived products...
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync("customer-1", "AUR-100", 5));

        // ...while removal (which never uses the active-only lookup) works.
        await service.RemoveAsync("customer-1", "AUR-100");
        Assert.Empty(await service.GetBasketItemsAsync("customer-1"));
    }

    [Fact]
    public async Task ConcurrentFirstAdd_SecondSaveThrowsBasketConflict()
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

        // Two requests race the first add: both observe "no basket item", both
        // insert, one wins. The loser must see the Core-owned conflict, never
        // an EF exception.
        await using var contextA = new RetailLabDbContext(options);
        await using var contextB = new RetailLabDbContext(options);
        var repositoryA = new EfRetailRepository(contextA);
        var repositoryB = new EfRetailRepository(contextB);

        var productA = await repositoryA.FindProductBySkuAsync("AUR-100");
        var productB = await repositoryB.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(productA);
        Assert.NotNull(productB);

        Assert.Null(await repositoryA.FindBasketItemAsync("customer-1", productA.Id));
        Assert.Null(await repositoryB.FindBasketItemAsync("customer-1", productB.Id));

        repositoryA.AddBasketItem(new BasketItem("customer-1", productA, 1, Now));
        repositoryB.AddBasketItem(new BasketItem("customer-1", productB, 1, Now));

        await repositoryA.SaveBasketChangesAsync();

        var conflict = await Assert.ThrowsAsync<BasketConflictException>(
            () => repositoryB.SaveBasketChangesAsync());
        Assert.Equal("Your basket changed; please try again.", conflict.Message);
        Assert.IsType<DbUpdateException>(conflict.InnerException);

        // The translated failure really is the expected primary-key collision.
        var sqlite = Assert.IsType<SqliteException>(conflict.InnerException.InnerException);
        Assert.Equal(SqliteConstraintPrimaryKey, sqlite.SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task ConcurrentUpdate_StaleSaveThrowsBasketConflict()
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

        // Request B loads the row first and goes stale while request A saves.
        // B's save must lose the Version check and surface the Core-owned
        // conflict instead of silently overwriting A's quantity.
        await using var contextA = new RetailLabDbContext(options);
        await using var contextB = new RetailLabDbContext(options);
        var repositoryB = new EfRetailRepository(contextB);

        var productB = await repositoryB.FindProductBySkuAsync("AUR-100");
        Assert.NotNull(productB);
        var stale = await repositoryB.FindBasketItemAsync("customer-1", productB.Id);
        Assert.NotNull(stale);

        await new BasketService(new EfRetailRepository(contextA), new TestTimeProvider(Now))
            .UpdateAsync("customer-1", "AUR-100", 5);

        stale.SetQuantity(7, Now);

        var conflict = await Assert.ThrowsAsync<BasketConflictException>(
            () => repositoryB.SaveBasketChangesAsync());
        Assert.Equal("Your basket changed; please try again.", conflict.Message);
        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);

        await using var verificationContext = new RetailLabDbContext(options);
        var winner = await new EfRetailRepository(verificationContext)
            .FindBasketItemAsync("customer-1", productB.Id);
        Assert.NotNull(winner);
        Assert.Equal(5, winner.Quantity);
    }

    [Fact]
    public async Task UnexpectedBasketConstraintFailure_PropagatesAsDbUpdateException()
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
            () => repository.SaveBasketChangesAsync());
        Assert.IsType<DbUpdateException>(failure);
        var sqlite = Assert.IsType<SqliteException>(failure.InnerException);
        Assert.Equal(SqliteConstraintCheck, sqlite.SqliteExtendedErrorCode);
    }
}
