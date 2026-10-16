using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Sync;

namespace RetailLab.LabCli;

public static class SyncCommand
{
    private const int SuccessExitCode = 0;
    private const int UnexpectedFailureExitCode = 1;
    private const int ConfigurationExitCode = 2;
    private const int ServerFailureExitCode = 3;
    private const int SafeStopExitCode = 4;
    private const int CancelledExitCode = 130;

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParseArguments(args, out var commandOptions, out var argumentError))
        {
            if (argumentError is not null)
            {
                Console.Error.WriteLine(argumentError);
                Console.Error.WriteLine();
            }

            PrintHelp();
            return argumentError is null ? SuccessExitCode : ConfigurationExitCode;
        }

        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            var readKey = Environment.GetEnvironmentVariable(
                HttpSyncTransport.ReadKeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(readKey))
            {
                throw new SyncConfigurationException(
                    $"Set the {HttpSyncTransport.ReadKeyEnvironmentVariable} environment variable " +
                    "before synchronizing.");
            }

            var database = new RetailLabSqliteDatabase(commandOptions.DataDirectory);
            await using (var dbContext = database.CreateDbContext())
            {
                // A sync client shares the normal schema but deliberately skips
                // DatabaseInitializer so sample products are never seeded.
                await dbContext.Database.MigrateAsync(cancellationSource.Token);
            }

            var clientOptions = new SyncClientOptions(
                commandOptions.ServerBaseUrl,
                commandOptions.PageSize);
            using var httpClient = new HttpClient();
            var transport = new HttpSyncTransport(
                httpClient,
                clientOptions,
                readKey);
            var store = new EfLocalSyncStore(database.CreateDbContext);
            var orchestrator = new SyncOrchestrator(
                transport,
                store,
                clientOptions,
                TimeProvider.System);
            var progress = new InlineProgress<SyncProgress>(WriteProgress);

            Console.WriteLine($"Synchronizing with {clientOptions.ServerBaseUrl}");
            Console.WriteLine($"Client database: {database.DatabasePath}");

            var result = await orchestrator.RunAsync(progress, cancellationSource.Token);

            await using var finalContext = database.CreateDbContext();
            var productCount = await finalContext.Products.CountAsync(cancellationSource.Token);
            if (result.PagesApplied == 0)
            {
                Console.WriteLine(
                    $"Already up to date at feed sequence {result.Cursor}. Products: {productCount}.");
            }
            else
            {
                Console.WriteLine(
                    $"Caught up at feed sequence {result.Cursor}: " +
                    $"{result.EntriesApplied} change(s) applied across {result.PagesApplied} page(s). " +
                    $"Products: {productCount}.");
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            Console.Error.WriteLine("Synchronization cancelled. Any incomplete page was rolled back.");
            return CancelledExitCode;
        }
        catch (SyncConfigurationException exception)
        {
            return WriteFailure(exception.Message, ConfigurationExitCode);
        }
        catch (SyncAuthenticationException exception)
        {
            return WriteFailure(exception.Message, ConfigurationExitCode);
        }
        catch (SyncServerUnavailableException exception)
        {
            return WriteFailure(exception.Message, ServerFailureExitCode);
        }
        catch (SyncTimeoutException exception)
        {
            return WriteFailure(exception.Message, ServerFailureExitCode);
        }
        catch (SyncServerResponseException exception)
        {
            return WriteFailure(exception.Message, ServerFailureExitCode);
        }
        catch (DatabaseNotEligibleForPairingException exception)
        {
            return WriteFailure(
                $"{exception.Message} Products: {exception.ProductCount}; " +
                $"outbox entries: {exception.OutboxEntryCount}. " +
                "Choose a new empty --data-directory; this command did not delete or replace the database.",
                SafeStopExitCode);
        }
        catch (Exception exception) when (
            exception is SyncProtocolException or
                ServerIdentityChangedException or
                CursorBeyondHeadException or
                SyncFeedGapException or
                PendingLocalSyncWorkException or
                SyncCheckpointConflictException or
                SyncPageValidationException or
                SyncLocalStateException or
                SyncEligibilityException or
                BusinessRuleException)
        {
            return WriteFailure(exception.Message, SafeStopExitCode);
        }
        catch (Exception exception)
        {
            return WriteFailure(
                $"Synchronization failed unexpectedly: {exception.Message}",
                UnexpectedFailureExitCode);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static void WriteProgress(SyncProgress progress)
    {
        switch (progress)
        {
            case SyncPairedProgress paired:
                Console.WriteLine(
                    $"Paired with server {paired.ServerInstanceId:D}; " +
                    $"server head is {paired.HeadSequence}.");
                break;
            case SyncPageAppliedProgress page:
                Console.WriteLine(
                    $"Applied sequence {page.FirstSequence}-{page.LastSequence} " +
                    $"({page.EntryCount} change(s)); observed head {page.ObservedHead}.");
                break;
        }
    }

    private static int WriteFailure(string message, int exitCode)
    {
        Console.Error.WriteLine(message);
        return exitCode;
    }

    private static bool TryParseArguments(
        string[] args,
        out CommandOptions options,
        out string? error)
    {
        options = null!;
        error = null;
        Uri? serverBaseUrl = null;
        string? dataDirectory = null;
        var pageSize = 100;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h")
            {
                return false;
            }

            if (argument is not ("--server" or "--data-directory" or "--page-size"))
            {
                error = $"Unknown option: {argument}.";
                return false;
            }

            if (index + 1 >= args.Length)
            {
                error = $"Missing value for {argument}.";
                return false;
            }

            var value = args[++index];
            switch (argument)
            {
                case "--server":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out serverBaseUrl) ||
                        (!serverBaseUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                         !serverBaseUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                    {
                        error = "--server must be an absolute HTTP or HTTPS URL.";
                        return false;
                    }

                    break;
                case "--data-directory":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        error = "--data-directory must not be empty.";
                        return false;
                    }

                    try
                    {
                        dataDirectory = Path.GetFullPath(value);
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        error = $"--data-directory is not a valid path: {exception.Message}";
                        return false;
                    }

                    break;
                case "--page-size":
                    if (!int.TryParse(value, out pageSize) ||
                        pageSize is <= 0 or > SyncClientOptions.MaximumPageSize)
                    {
                        error = $"--page-size must be between 1 and {SyncClientOptions.MaximumPageSize}.";
                        return false;
                    }

                    break;
            }
        }

        if (serverBaseUrl is null)
        {
            error = "The --server option is required.";
            return false;
        }

        if (dataDirectory is null)
        {
            error = "The --data-directory option is required.";
            return false;
        }

        options = new CommandOptions(serverBaseUrl, dataDirectory, pageSize);
        return true;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("RetailLab headless catalogue synchronization");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  dotnet run --project src/RetailLab.LabCli -- sync " +
            "--server <url> --data-directory <path> [--page-size <1-500>]");
        Console.WriteLine();
        Console.WriteLine(
            $"Set {HttpSyncTransport.ReadKeyEnvironmentVariable} to the server's read key first.");
        Console.WriteLine("Use a new, dedicated data directory for the first pairing.");
    }

    private sealed record CommandOptions(Uri ServerBaseUrl, string DataDirectory, int PageSize);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
