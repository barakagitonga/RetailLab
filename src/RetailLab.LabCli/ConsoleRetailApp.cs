using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.LabCli;

public sealed class ConsoleRetailApp(
    Func<RetailLabDbContext> createDbContext,
    TimeProvider timeProvider)
{
    private const string CustomerId = "customer-demo-001";

    public async Task RunAsync()
    {
        Console.WriteLine("RetailLab - Lab Prototype 1");
        Console.WriteLine($"Simulated customer: {CustomerId}");

        while (true)
        {
            WriteMenu();
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        await DisplayProductsAsync(includePrice: true);
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
                        await DisplayProductsAsync(includePrice: false);
                        break;
                    case "0":
                    case null:
                        Console.WriteLine("Goodbye.");
                        return;
                    default:
                        Console.WriteLine("Choose a number from 0 to 7.");
                        break;
                }
            }
            catch (BusinessRuleException exception)
            {
                Console.WriteLine($"Unable to complete that action: {exception.Message}");
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
        Console.WriteLine("7. View remaining inventory");
        Console.WriteLine("0. Exit");
        Console.Write("Choice: ");
    }

    private async Task DisplayProductsAsync(bool includePrice)
    {
        await using var dbContext = createDbContext();
        var repository = new EfRetailRepository(dbContext);
        var products = await repository.GetProductsAsync();

        Console.WriteLine(includePrice ? "\nProducts" : "\nRemaining inventory");
        Console.WriteLine(new string('-', includePrice ? 82 : 55));

        foreach (var product in products)
        {
            if (includePrice)
            {
                Console.WriteLine(
                    $"{product.Sku,-10} {product.Description,-38} " +
                    $"Price: {product.Price,8:0.00}  Stock: {product.StockQuantity,3}");
            }
            else
            {
                Console.WriteLine($"{product.Sku,-10} {product.Description,-32} {product.StockQuantity,3}");
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

    private static string ReadRequired(string prompt)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}
