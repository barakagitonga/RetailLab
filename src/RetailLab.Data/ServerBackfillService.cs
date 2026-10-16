using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Prepares pre-7C server databases without enabling automatic publication on
/// ordinary local contexts. Each attempt uses a fresh base context so a failed
/// SQLite transaction cannot leak tracked state into a retry.
/// </summary>
public sealed class ServerBackfillService(RetailLabSqliteDatabase database)
{
    private const int MaximumAttempts = 3;

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        await BackfillProductsAsync(cancellationToken);
        await EnsureServerIdentityAsync(cancellationToken);
    }

    public async Task<Guid> GetServerInstanceIdAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = database.CreateDbContext();
        var properties = await dbContext.SyncServerProperties
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return properties?.ServerInstanceId
            ?? throw new InvalidOperationException("The server database has not been prepared for synchronization.");
    }

    private async Task BackfillProductsAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                await using var dbContext = database.CreateDbContext();
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                var products = await dbContext.Products
                    .Where(product => product.ServerRevision == 0)
                    .OrderBy(product => product.Sku)
                    .ToListAsync(cancellationToken);

                if (products.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var occurredAtUtc = DateTimeOffset.UtcNow;
                foreach (var product in products)
                {
                    product.ApplyServerRevision(1);
                    var snapshot = ProductSnapshot.FromProduct(product);
                    dbContext.SyncChangeFeedEntries.Add(
                        new SyncChangeFeedEntry(
                            product.Id,
                            ProductChangeKind.Created,
                            product.ServerRevision,
                            JsonSerializer.Serialize(snapshot),
                            occurredAtUtc));
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (IsRetryableRace(exception))
            {
                if (!await HasUnbackfilledProductsAsync(cancellationToken))
                {
                    return;
                }

                if (attempt == MaximumAttempts)
                {
                    throw new InvalidOperationException(
                        "The server product feed could not be backfilled after multiple concurrent startup attempts.",
                        exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    private async Task EnsureServerIdentityAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                await using var dbContext = database.CreateDbContext();
                if (await dbContext.SyncServerProperties.AnyAsync(cancellationToken))
                {
                    return;
                }

                dbContext.SyncServerProperties.Add(
                    new SyncServerProperties(Guid.NewGuid(), DateTimeOffset.UtcNow));
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (IsRetryableRace(exception))
            {
                if (await HasServerIdentityAsync(cancellationToken))
                {
                    return;
                }

                if (attempt == MaximumAttempts)
                {
                    throw new InvalidOperationException(
                        "The server identity could not be created after multiple concurrent startup attempts.",
                        exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    private async Task<bool> HasUnbackfilledProductsAsync(CancellationToken cancellationToken)
    {
        await using var verificationContext = database.CreateDbContext();
        return await verificationContext.Products
            .AnyAsync(product => product.ServerRevision == 0, cancellationToken);
    }

    private async Task<bool> HasServerIdentityAsync(CancellationToken cancellationToken)
    {
        await using var verificationContext = database.CreateDbContext();
        return await verificationContext.SyncServerProperties.AnyAsync(cancellationToken);
    }

    private static bool IsRetryableRace(Exception exception)
    {
        var sqlite = exception switch
        {
            SqliteException direct => direct,
            DbUpdateException { InnerException: SqliteException inner } => inner,
            _ => null
        };

        return sqlite?.SqliteErrorCode is 5 or 6 or 19;
    }
}
