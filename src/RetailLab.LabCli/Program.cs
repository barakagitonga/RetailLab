using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Data;
using RetailLab.LabCli;

try
{
    var configuredDataDirectory = Environment.GetEnvironmentVariable("RETAILLAB_DATA_DIRECTORY");
    var databaseDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetailLab",
            "LabPrototype1")
        : Path.GetFullPath(configuredDataDirectory);

    Directory.CreateDirectory(databaseDirectory);

    var databasePath = Path.Combine(databaseDirectory, "retaillab.db");
    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        ForeignKeys = true
    }.ToString();

    RetailLabDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new RetailLabDbContext(options);
    }

    await using (var dbContext = CreateDbContext())
    {
        await DatabaseInitializer.InitializeAsync(dbContext);
    }

    var app = new ConsoleRetailApp(CreateDbContext, TimeProvider.System);
    await app.RunAsync();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"RetailLab could not start: {exception.Message}");
    Environment.ExitCode = 1;
}
