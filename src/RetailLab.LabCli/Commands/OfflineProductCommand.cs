using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Sync;

namespace RetailLab.LabCli;

/// <summary>
/// Minimal headless surface for demonstrating offline product edits. WPF
/// integration remains intentionally deferred beyond Tutorial 7D.
/// </summary>
public static class OfflineProductCommand
{
    public static async Task<int> RunAsync(string command, string[] args)
    {
        if (!TryReadOptions(args, out var values, out var error) || values.ContainsKey("help"))
        {
            if (error is not null)
            {
                Console.Error.WriteLine(error);
            }
            PrintHelp(command);
            return error is null ? 0 : 2;
        }

        try
        {
            var dataDirectory = Require(values, "data-directory");
            var database = new RetailLabSqliteDatabase(Path.GetFullPath(dataDirectory));
            await using (var context = database.CreateDbContext())
            {
                await context.Database.MigrateAsync();
            }

            var store = new EfLocalSyncStore(database.CreateDbContext);
            var service = new OfflineProductOperationService(store, TimeProvider.System);
            ISyncProductOperation operation = command switch
            {
                "create-product" => await service.CreateAsync(
                    Require(values, "sku"),
                    Require(values, "description"),
                    ReadDecimal(values, "price"),
                    ReadInt32(values, "stock")),
                "update-product" => await service.UpdateAsync(
                    ReadGuid(values, "product-id"),
                    Require(values, "description"),
                    ReadDecimal(values, "price")),
                "archive-product" => await service.ArchiveAsync(ReadGuid(values, "product-id")),
                "unarchive-product" => await service.UnarchiveAsync(ReadGuid(values, "product-id")),
                "adjust-stock" => await service.AdjustStockAsync(
                    ReadGuid(values, "product-id"),
                    ReadInt32(values, "delta"),
                    Require(values, "reason")),
                _ => throw new ArgumentException("Unknown product command.")
            };

            Console.WriteLine(
                $"Captured {operation.Kind} operation {operation.OperationId:D} " +
                $"for product {operation.ProductId:D}.");
            Console.WriteLine("Run the sync command when the server is reachable.");
            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or BusinessRuleException or SyncLocalStateException or DbUpdateException)
        {
            Console.Error.WriteLine($"Offline product change was not saved: {exception.Message}");
            return 1;
        }
    }

    private static bool TryReadOptions(
        string[] args,
        out Dictionary<string, string> values,
        out string? error)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h")
            {
                values["help"] = string.Empty;
                return true;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
            {
                error = $"Unexpected argument: {argument}.";
                return false;
            }

            var name = argument[2..];
            if (values.ContainsKey(name))
            {
                error = $"Option {argument} was supplied more than once.";
                return false;
            }

            if (++index >= args.Length)
            {
                error = $"Missing value for {argument}.";
                return false;
            }

            values[name] = args[index];
        }

        return true;
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The --{name} option is required.");
        }
        return value;
    }

    private static Guid ReadGuid(IReadOnlyDictionary<string, string> values, string name)
    {
        var text = Require(values, name);
        return Guid.TryParse(text, out var value) && value != Guid.Empty
            ? value
            : throw new ArgumentException($"--{name} must be a non-empty GUID.");
    }

    private static decimal ReadDecimal(IReadOnlyDictionary<string, string> values, string name)
    {
        var text = Require(values, name);
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"--{name} must be a decimal number using '.' as the separator.");
    }

    private static int ReadInt32(IReadOnlyDictionary<string, string> values, string name)
    {
        var text = Require(values, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"--{name} must be a whole number.");
    }

    private static void PrintHelp(string command)
    {
        Console.WriteLine("RetailLab offline product capture");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine(command switch
        {
            "create-product" =>
                "  ... create-product --data-directory <client-data> --sku <sku> " +
                "--description <text> --price <amount> --stock <quantity>",
            "update-product" =>
                "  ... update-product --data-directory <client-data> --product-id <guid> " +
                "--description <text> --price <amount>",
            "archive-product" =>
                "  ... archive-product --data-directory <client-data> --product-id <guid>",
            "unarchive-product" =>
                "  ... unarchive-product --data-directory <client-data> --product-id <guid>",
            "adjust-stock" =>
                "  ... adjust-stock --data-directory <client-data> --product-id <guid> " +
                "--delta <signed-quantity> --reason <restock|damage|shrinkage|correction>",
            _ => "  Unknown command."
        });
    }
}
