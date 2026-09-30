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

    var database = new RetailLabSqliteDatabase(databaseDirectory);

    RetailLabDbContext CreateDbContext() => database.CreateDbContext();

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
