using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RetailLab.Data;

/// <summary>
/// Owns the SQLite-specific setup shared by local RetailLab applications.
/// Each operation still creates a short-lived DbContext so tracked entities
/// from one screen action cannot leak into the next one.
/// </summary>
public sealed class RetailLabSqliteDatabase
{
    public RetailLabSqliteDatabase(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(DataDirectory);

        DatabasePath = Path.Combine(DataDirectory, "retaillab.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true
        }.ToString();
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public RetailLabDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        return new RetailLabDbContext(options);
    }
}
