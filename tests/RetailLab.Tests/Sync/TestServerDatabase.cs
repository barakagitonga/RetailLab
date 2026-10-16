using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using RetailLab.Data;

namespace RetailLab.Tests.Sync;

internal sealed class TestServerDatabase : IAsyncDisposable
{
    private TestServerDatabase(string directory)
    {
        Directory = directory;
        Database = new RetailLabSqliteDatabase(directory);
    }

    private string Directory { get; }

    public RetailLabSqliteDatabase Database { get; }

    public static async Task<TestServerDatabase> CreateAsync()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "RetailLab.Tests",
            $"server-sync-{Guid.NewGuid():N}");
        var database = new TestServerDatabase(directory);

        await using var context = database.CreateBaseContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public RetailLabDbContext CreateBaseContext() => Database.CreateDbContext();

    public ServerRetailDbContext CreateServerContext() => Database.CreateServerDbContext();

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
