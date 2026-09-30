using System.Globalization;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.LabCli;

public sealed class ConsoleRetailApp(
    Func<RetailLabDbContext> createDbContext,
    TimeProvider timeProvider)
{
    private const string CustomerId = "customer-demo-001";
    private const string StaffActor = "staff-demo-01";

    public async Task RunAsync()
    {
        Console.WriteLine("RetailLab - Lab Prototype 1");
        Console.WriteLine($"Simulated customer: {CustomerId}");
        Console.WriteLine($"Simulated staff actor: {StaffActor}");

        while (true)
        {
            WriteMenu();
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        await DisplayProductsAsync(includePrice: true, includeArchived: false);
                        break;
                    case "2":
                        await AddBookmarkAsync();
                        break;
                    case "3":
                        await DisplayBookmarksAsync();
                        break;
                    case "4":
                        await RemoveBookmarkAsync();
                        break;
                    case "5":
                        await PlaceOrderAsync();
                        break;
                    case "6":
                        await DisplayOrdersAsync();
                        break;
                    case "7":
                        await DisplayProductsAsync(includePrice: false, includeArchived: true);
                        break;
                    case "8":
                        await CreateProductAsync();
                        break;
                    case "9":
                        await UpdateProductAsync();
                        break;
                    case "10":
                        await ArchiveOrUnarchiveProductAsync();
                        break;
                    case "11":
                        await AdjustStockAsync();
                        break;
                    case "12":
                        await DisplayAdjustmentHistoryAsync();
                        break;
                    case "0":
                    case null:
                        Console.WriteLine("Goodbye.");
                        return;
                    default:
                        Console.WriteLine("Choose a number from 0 to 12.");
                        break;
                }
            }
            catch (BusinessRuleException exception)
            {
                Console.WriteLine($"Unable to complete that action: {exception.Message}");
            }
            catch (ProductConflictException exception)
            {
                Console.WriteLine($"Please try again: {exception.Message}");
            }
            catch (ArgumentException exception)
            {
                Console.WriteLine($"Invalid input: {exception.Message}");
            }
        }
    }

    private static void WriteMenu()
    {
        Console.WriteLine();
        Console.WriteLine("1. Display products");
        Console.WriteLine("2. Bookmark a product");
        Console.WriteLine("3. View bookmarks");
        Console.WriteLine("4. Remove a bookmark");
        Console.WriteLine("5. Place a simulated order");
        Console.WriteLine("6. View previous orders");
        Console.WriteLine("7. View remaining inventory (staff, includes archived)");
        Console.WriteLine("8. Create product (staff)");
        Console.WriteLine("9. Update product details (staff)");
        Console.WriteLine("10. Archive or unarchive product (staff)");
        Console.WriteLine("11. Adjust stock (staff)");
        Console.WriteLine("12. View stock adjustment history (staff)");
        Console.WriteLine("0. Exit");
        Console.Write("Choice: ");
    }

    private async Task DisplayProductsAsync(bool includePrice, bool includeArchived)
    {
        await using var dbContext = createDbContext();
        var repository = new EfRetailRepository(dbContext);
        var products = await repository.GetProductsAsync(includeArchived);

        Console.WriteLine(includePrice ? "\nProducts" : "\nRemaining inventory");
        Console.WriteLine(new string('-', includePrice ? 82 : 64));

        foreach (var product in products)
        {
            var archivedMarker = product.IsArchived ? " [ARCHIVED]" : string.Empty;

            if (includePrice)
            {
                Console.WriteLine(
                    $"{product.Sku,-10} {product.Description,-38} " +
                    $"Price: {product.Price,8:0.00}  Stock: {product.StockQuantity,3}{archivedMarker}");
            }
            else
            {
                Console.WriteLine($"{product.Sku,-10} {product.Description,-32} {product.StockQuantity,3}{archivedMarker}");
            }
        }
    }

    private async Task AddBookmarkAsync()
    {
        var sku = ReadRequired("SKU to bookmark: ");

        await using var dbContext = createDbContext();
        var service = new BookmarkService(new EfRetailRepository(dbContext), timeProvider);
        await service.AddAsync(CustomerId, sku);

        Console.WriteLine($"{sku.ToUpperInvariant()} was bookmarked.");
    }

    private async Task DisplayBookmarksAsync()
    {
        await using var dbContext = createDbContext();
        var repository = new EfRetailRepository(dbContext);
        var bookmarks = await repository.GetBookmarksAsync(CustomerId);

        Console.WriteLine("\nBookmarks");
        if (bookmarks.Count == 0)
        {
            Console.WriteLine("No products are bookmarked.");
            return;
        }

        foreach (var bookmark in bookmarks)
        {
            Console.WriteLine(
                $"{bookmark.Product.Sku,-10} {bookmark.Product.Description,-38} " +
                $"Price: {bookmark.Product.Price,8:0.00}");
        }
    }

    private async Task RemoveBookmarkAsync()
    {
        var sku = ReadRequired("SKU to remove from bookmarks: ");

        await using var dbContext = createDbContext();
        var service = new BookmarkService(new EfRetailRepository(dbContext), timeProvider);
        await service.RemoveAsync(CustomerId, sku);

        Console.WriteLine($"{sku.ToUpperInvariant()} was removed from bookmarks.");
    }

    private async Task PlaceOrderAsync()
    {
        Console.WriteLine("Enter order lines. Leave the SKU blank when finished.");
        var requests = new List<OrderItemRequest>();

        while (true)
        {
            Console.Write("SKU: ");
            var sku = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(sku))
            {
                break;
            }

            Console.Write("Quantity: ");
            var quantityText = Console.ReadLine();
            if (!int.TryParse(quantityText, out var quantity) || quantity <= 0)
            {
                Console.WriteLine("Quantity must be a whole number greater than zero. This line was not added.");
                continue;
            }

            requests.Add(new OrderItemRequest(sku, quantity));
        }

        if (requests.Count == 0)
        {
            Console.WriteLine("Order cancelled because no items were entered.");
            return;
        }

        await using var dbContext = createDbContext();
        var service = new OrderService(new EfRetailRepository(dbContext), timeProvider);
        var order = await service.PlaceAsync(CustomerId, requests);

        Console.WriteLine($"Order {ShortId(order.Id)} placed. Total: {order.Total:0.00}");
    }

    private async Task DisplayOrdersAsync()
    {
        await using var dbContext = createDbContext();
        var repository = new EfRetailRepository(dbContext);
        var orders = await repository.GetOrdersAsync(CustomerId);

        Console.WriteLine("\nPrevious simulated orders");
        if (orders.Count == 0)
        {
            Console.WriteLine("No orders have been placed.");
            return;
        }

        foreach (var order in orders)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Order {ShortId(order.Id)} | {order.PlacedAtUtc:yyyy-MM-dd HH:mm:ss} UTC | Total: {order.Total:0.00}");

            foreach (var line in order.Lines.OrderBy(line => line.Sku))
            {
                Console.WriteLine(
                    $"  {line.Sku,-10} {line.ProductDescription,-34} " +
                    $"{line.Quantity,3} x {line.UnitPrice,8:0.00} = {line.LineTotal,9:0.00}");
            }
        }
    }

    private async Task CreateProductAsync()
    {
        var sku = ReadRequired("New product SKU: ");
        var description = ReadRequired("Description: ");
        var price = ReadDecimal("Price: ");
        var stockQuantity = ReadInt("Initial stock quantity: ");

        await using var dbContext = createDbContext();
        var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);
        var product = await service.CreateAsync(sku, description, price, stockQuantity);

        Console.WriteLine($"{product.Sku} was created with stock {product.StockQuantity}.");
    }

    private async Task UpdateProductAsync()
    {
        var sku = ReadRequired("SKU to update: ");
        var description = ReadRequired("New description: ");
        var price = ReadDecimal("New price: ");

        await using var dbContext = createDbContext();
        var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);
        var product = await service.UpdateDetailsAsync(sku, description, price);

        Console.WriteLine($"{product.Sku} was updated. Price: {product.Price:0.00}");
    }

    private async Task ArchiveOrUnarchiveProductAsync()
    {
        var sku = ReadRequired("SKU to archive or unarchive: ");
        Console.Write("Action (archive/unarchive): ");
        var action = Console.ReadLine()?.Trim().ToLowerInvariant();

        await using var dbContext = createDbContext();
        var service = new ProductService(new EfRetailRepository(dbContext), timeProvider);

        if (action == "archive")
        {
            await service.ArchiveAsync(sku);
            Console.WriteLine($"{sku.ToUpperInvariant()} was archived.");
        }
        else if (action == "unarchive")
        {
            await service.UnarchiveAsync(sku);
            Console.WriteLine($"{sku.ToUpperInvariant()} was unarchived.");
        }
        else
        {
            Console.WriteLine("Action must be 'archive' or 'unarchive'. Nothing was changed.");
        }
    }

    private async Task AdjustStockAsync()
    {
        var sku = ReadRequired("SKU to adjust: ");
        var delta = ReadInt("Quantity change (signed, e.g. 5 or -3): ");
        var reason = ReadRequired("Reason for the adjustment: ");

        await using var dbContext = createDbContext();
        var service = new InventoryService(new EfRetailRepository(dbContext), timeProvider);
        var adjustment = await service.AdjustAsync(sku, delta, reason, StaffActor);

        Console.WriteLine(
            $"{adjustment.QuantityChange:+0;-0} applied. New stock: {adjustment.ResultingQuantity}.");
    }

    private async Task DisplayAdjustmentHistoryAsync()
    {
        var sku = ReadRequired("SKU to inspect: ");

        await using var dbContext = createDbContext();
        var service = new InventoryService(new EfRetailRepository(dbContext), timeProvider);
        var history = await service.GetHistoryAsync(sku);

        Console.WriteLine($"\nStock adjustment history for {sku.ToUpperInvariant()}");
        if (history.Count == 0)
        {
            Console.WriteLine("No adjustments have been recorded.");
            return;
        }

        foreach (var adjustment in history)
        {
            Console.WriteLine(
                $"{adjustment.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC | " +
                $"{adjustment.QuantityChange:+0;-0} -> {adjustment.ResultingQuantity,3} | " +
                $"{adjustment.Reason} ({adjustment.ActorIdentifier})");
        }
    }

    private static string ReadRequired(string prompt)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static decimal ReadDecimal(string prompt)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException("A numeric price was expected (for example 9.99).");
        }

        return parsed;
    }

    private static int ReadInt(string prompt)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException("A whole number was expected.");
        }

        return parsed;
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}
