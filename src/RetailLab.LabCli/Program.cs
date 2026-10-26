using RetailLab.Data;
using RetailLab.LabCli;

if (args.Length > 0 && args[0].Equals("sync", StringComparison.OrdinalIgnoreCase))
{
    return await SyncCommand.RunAsync(args[1..]);
}

if (args.Length > 0 && args[0] is "enroll-device" or "rotate-device" or "revoke-device")
{
    return await DeviceCommand.RunAsync(args[0], args[1..]);
}

if (args.Length > 0 && args[0] is "create-product" or "update-product" or "archive-product" or "unarchive-product" or "adjust-stock")
{
    return await OfflineProductCommand.RunAsync(args[0], args[1..]);
}

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
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"RetailLab could not start: {exception.Message}");
    return 1;
}
