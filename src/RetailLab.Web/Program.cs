using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Standard ASP.NET Core Identity with the Entity Framework store.
// UserManager and SignInManager own password hashing and cookie sign-in;
// accounts persist in the framework-managed AspNet* tables. Unique email
// addresses; confirmed accounts are not required for this local demo.
// Cookie paths use the Identity defaults (/Account/Login and friends).
builder.Services.AddAuthorization();
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<RetailLabDbContext>();

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
builder.Services.AddScoped<BookmarkService>();
builder.Services.AddSingleton(TimeProvider.System);
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
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
return 0;
