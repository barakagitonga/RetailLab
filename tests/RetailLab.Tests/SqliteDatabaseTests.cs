using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class SqliteDatabaseTests
{
    [Fact]
    public async Task CreateDbContext_UsesOneInitializedFileAcrossContexts()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "RetailLab.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var database = new RetailLabSqliteDatabase(directory);

            Assert.Equal(Path.GetFullPath(directory), database.DataDirectory);
            Assert.Equal(Path.Combine(Path.GetFullPath(directory), "retaillab.db"), database.DatabasePath);

            await using (var initializationContext = database.CreateDbContext())
            {
                await DatabaseInitializer.InitializeAsync(initializationContext);
            }

            await using (var verificationContext = database.CreateDbContext())
            {
                Assert.Equal(5, await verificationContext.Products.CountAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
