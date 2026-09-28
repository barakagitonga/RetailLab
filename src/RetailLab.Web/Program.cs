using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Same database resolution as LabCli so the console and the website can
// deliberately share one local demonstration database.
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

builder.Services.AddDbContext<RetailLabDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IRetailRepository, EfRetailRepository>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddSingleton<CatalogDisplayMapper>();

var app = builder.Build();

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<RetailLabDbContext>();
        await DatabaseInitializer.InitializeAsync(dbContext);
    }
}
catch (Exception exception)
{
    // Startup failure: log the full detail for the operator, tell the user only
    // that the catalogue is unavailable, and do not start serving requests.
    app.Logger.LogCritical(exception, "RetailLab database initialization failed for {DatabasePath}", databasePath);
    Console.Error.WriteLine("RetailLab could not start: the catalogue database is unavailable.");
    return 1;
}

// Request-time failures (after successful startup) render the friendly /Error page.
app.UseExceptionHandler("/Error");
app.UseHsts();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
return 0;
