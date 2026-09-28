using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Web.Pages.Account;

namespace RetailLab.Tests;

/// <summary>
/// End-to-end coverage of the standard Identity account wiring (UserManager
/// and SignInManager over the EF store) plus per-customer favourite
/// isolation using real Identity user ids.
/// </summary>
public sealed class IdentityAccountTests
{
    [Fact]
    public async Task Register_CreatesUserThroughUserManager()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var user = new IdentityUser
        {
            UserName = "sam@example.com",
            Email = "sam@example.com",
            EmailConfirmed = true
        };
        var created = await server.Users.CreateAsync(user, "Demo-pass-1");

        Assert.True(created.Succeeded);
        var found = await server.Users.FindByEmailAsync("sam@example.com");
        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal("sam@example.com", found.UserName);
    }

    [Fact]
    public async Task StoredPassword_IsHashedNotPlaintext()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var user = new IdentityUser
        {
            UserName = "sam@example.com",
            Email = "sam@example.com",
            EmailConfirmed = true
        };
        Assert.True((await server.Users.CreateAsync(user, "Demo-pass-1")).Succeeded);

        Assert.True(await server.Users.HasPasswordAsync(user));

        var stored = await server.Db.Users.AsNoTracking()
            .SingleAsync(storedUser => storedUser.Id == user.Id);
        Assert.NotNull(stored.PasswordHash);
        Assert.NotEqual("Demo-pass-1", stored.PasswordHash);
        Assert.True(await server.Users.CheckPasswordAsync(user, "Demo-pass-1"));
        Assert.False(await server.Users.CheckPasswordAsync(user, "Wrong-pass-9"));
    }

    [Fact]
    public async Task ValidPassword_SignsIn()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var user = new IdentityUser
        {
            UserName = "sam@example.com",
            Email = "sam@example.com",
            EmailConfirmed = true
        };
        Assert.True((await server.Users.CreateAsync(user, "Demo-pass-1")).Succeeded);

        // Credential verification only; cookie issuance is framework behavior
        // exercised by the login probes against the running site.
        var result = await server.SignIn.CheckPasswordSignInAsync(
            user,
            "Demo-pass-1",
            lockoutOnFailure: true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task InvalidCredentials_RejectedWithoutRevealingEmail()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var user = new IdentityUser
        {
            UserName = "sam@example.com",
            Email = "sam@example.com",
            EmailConfirmed = true
        };
        Assert.True((await server.Users.CreateAsync(user, "Demo-pass-1")).Succeeded);

        Assert.Null(await server.Users.FindByEmailAsync("nobody@example.com"));

        var existing = await server.Users.FindByEmailAsync("sam@example.com");
        Assert.NotNull(existing);
        var wrongPassword = await server.SignIn.CheckPasswordSignInAsync(
            existing,
            "Wrong-pass-9",
            lockoutOnFailure: true);
        Assert.False(wrongPassword.Succeeded);
        Assert.False(wrongPassword.IsLockedOut);

        // Page level: unknown email and wrong password surface the same
        // generic message, so neither reveals whether the email exists.
        var unknownPage = server.CreateLoginPage("nobody@example.com", "Whatever-1");
        Assert.IsType<PageResult>(await unknownPage.OnPostAsync(returnUrl: null));

        var wrongPage = server.CreateLoginPage("sam@example.com", "Wrong-pass-9");
        Assert.IsType<PageResult>(await wrongPage.OnPostAsync(returnUrl: null));

        Assert.Equal("Invalid email or password.", SingleError(unknownPage));
        Assert.Equal(SingleError(unknownPage), SingleError(wrongPage));
    }

    [Fact]
    public async Task RepeatedFailures_ParticipateInLockout()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var user = new IdentityUser
        {
            UserName = "sam@example.com",
            Email = "sam@example.com",
            EmailConfirmed = true
        };
        Assert.True((await server.Users.CreateAsync(user, "Demo-pass-1")).Succeeded);

        var sawLockout = false;
        for (var attempt = 0; attempt < 10 && !sawLockout; attempt++)
        {
            var result = await server.SignIn.CheckPasswordSignInAsync(
                user,
                "Wrong-pass-9",
                lockoutOnFailure: true);
            sawLockout = result.IsLockedOut;
        }

        Assert.True(sawLockout);
        Assert.True(await server.Users.IsLockedOutAsync(user));

        var correctWhileLocked = await server.SignIn.CheckPasswordSignInAsync(
            user,
            "Demo-pass-1",
            lockoutOnFailure: true);
        Assert.True(correctWhileLocked.IsLockedOut);
    }

    [Fact]
    public async Task Favourites_IsolatedBetweenIdentityCustomers()
    {
        await using var server = await IdentityTestServer.CreateAsync();

        var anna = new IdentityUser
        {
            UserName = "anna@example.com",
            Email = "anna@example.com",
            EmailConfirmed = true
        };
        var bob = new IdentityUser
        {
            UserName = "bob@example.com",
            Email = "bob@example.com",
            EmailConfirmed = true
        };
        Assert.True((await server.Users.CreateAsync(anna, "Demo-pass-1")).Succeeded);
        Assert.True((await server.Users.CreateAsync(bob, "Demo-pass-2")).Succeeded);

        await server.Bookmarks.AddAsync(anna.Id, "AUR-100");
        await server.Bookmarks.AddAsync(bob.Id, "AUR-100");

        Assert.Single(await server.Bookmarks.GetBookmarksAsync(anna.Id));
        Assert.Single(await server.Bookmarks.GetBookmarksAsync(bob.Id));

        await server.Bookmarks.RemoveAsync(anna.Id, "AUR-100");

        Assert.Empty(await server.Bookmarks.GetBookmarksAsync(anna.Id));
        var bobs = Assert.Single(await server.Bookmarks.GetBookmarksAsync(bob.Id));
        Assert.Equal("AUR-100", bobs.Product.Sku);
    }

    private static string SingleError(LoginModel page)
    {
        var entry = page.ModelState[string.Empty];
        Assert.NotNull(entry);
        return Assert.Single(entry.Errors).ErrorMessage;
    }

    /// <summary>
    /// Production-shaped service wiring over an isolated in-memory database:
    /// the same AddIdentity + EF store options the website registers.
    /// </summary>
    private sealed class IdentityTestServer : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public UserManager<IdentityUser> Users { get; }

        public SignInManager<IdentityUser> SignIn { get; }

        public RetailLabDbContext Db { get; }

        public BookmarkService Bookmarks { get; }

        public IHttpContextAccessor HttpContextAccessor { get; }

        private IdentityTestServer(
            SqliteConnection connection,
            ServiceProvider provider,
            IServiceScope scope)
        {
            _connection = connection;
            _provider = provider;
            _scope = scope;
            Users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            SignIn = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
            Db = scope.ServiceProvider.GetRequiredService<RetailLabDbContext>();
            Bookmarks = scope.ServiceProvider.GetRequiredService<BookmarkService>();
            HttpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        }

        public static async Task<IdentityTestServer> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<RetailLabDbContext>(
                options => options.UseSqlite(connection));
            services
                .AddIdentity<IdentityUser, IdentityRole>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedAccount = false;
                })
                .AddEntityFrameworkStores<RetailLabDbContext>();
            services.AddScoped<IRetailRepository, EfRetailRepository>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<BookmarkService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var server = new IdentityTestServer(connection, provider, scope);
            await DatabaseInitializer.InitializeAsync(server.Db);
            return server;
        }

        public LoginModel CreateLoginPage(string email, string password)
        {
            var httpContext = new DefaultHttpContext();
            HttpContextAccessor.HttpContext = httpContext;
            var page = new LoginModel(SignIn)
            {
                Input = new LoginModel.InputModel { Email = email, Password = password },
                PageContext = new PageContext(
                    new ActionContext(httpContext, new RouteData(), new PageActionDescriptor()))
            };
            return page;
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
