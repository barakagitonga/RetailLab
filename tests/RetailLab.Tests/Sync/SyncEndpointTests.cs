using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Web.Sync;

namespace RetailLab.Tests.Sync;

public sealed class SyncEndpointTests
{
    private const string ReadKey = "endpoint-test-read-key";

    [Fact]
    public async Task Pair_RejectsMissingAndIncorrectKeys()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var request = new PairRequest(Guid.NewGuid(), DeclaresEmpty: true);

        using var missingKeyResponse = await server.Client.PostAsJsonAsync(
            "/api/sync/v1/pair",
            request);

        using var wrongKeyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sync/v1/pair")
        {
            Content = JsonContent.Create(request)
        };
        wrongKeyRequest.Headers.Add(RequireSyncKeyFilter.HeaderName, "wrong-key");
        using var wrongKeyResponse = await server.Client.SendAsync(wrongKeyRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKeyResponse.StatusCode);
    }

    [Fact]
    public async Task Pair_ReturnsStableServerIdentityAndCurrentFeedHead()
    {
        await using var server = await SyncApiTestServer.CreateAsync();

        using var response = await server.PairAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PairResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ServerInstanceId);
        Assert.Equal(5, body.HeadSequence);
        Assert.True(body.ServerTimeUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Pair_RejectsClientThatDoesNotDeclareAnEmptyDatabase()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/v1/pair")
        {
            Content = JsonContent.Create(new PairRequest(Guid.NewGuid(), DeclaresEmpty: false))
        };

        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("client_not_empty", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Pull_ReturnsOrderedPageAndPagingMetadata()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/sync/v1/pull?clientInstanceId={Guid.NewGuid()}&serverInstanceId={pairing.ServerInstanceId}&after=0&limit=2");
        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PullResponse>();
        Assert.NotNull(body);
        Assert.Equal(5, body.HeadSequence);
        Assert.True(body.HasMore);
        Assert.Equal([1L, 2L], body.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public async Task Pull_RejectsChangedServerIdentity()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/sync/v1/pull?clientInstanceId={Guid.NewGuid()}&serverInstanceId={Guid.NewGuid()}&after=0");

        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("server_identity_changed", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Pull_RejectsCursorBeyondCurrentHead()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/sync/v1/pull?clientInstanceId={Guid.NewGuid()}&serverInstanceId={pairing.ServerInstanceId}&after={pairing.HeadSequence + 1}");

        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cursor_beyond_head", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Pull_RejectsGapWithinReturnedPage()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        await server.DeleteFeedEntryAsync(sequence: 2);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/sync/v1/pull?clientInstanceId={Guid.NewGuid()}&serverInstanceId={pairing.ServerInstanceId}&after=0&limit=5");

        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("feed_gap", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Pull_AfterServerMutationReturnsOnlyNewFeedEntry()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();

        await server.UpdateFirstProductAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/sync/v1/pull?clientInstanceId={Guid.NewGuid()}&serverInstanceId={pairing.ServerInstanceId}&after={pairing.HeadSequence}");
        using var response = await server.SendAuthorizedAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PullResponse>();
        var entry = Assert.Single(Assert.IsType<PullResponse>(body).Entries);
        Assert.Equal(pairing.HeadSequence + 1, entry.Sequence);
        Assert.Equal(2, entry.ServerRevision);
        Assert.Equal("Changed through the HTTP endpoint test", entry.Snapshot.Description);
    }

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private sealed class SyncApiTestServer : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private readonly TestServerDatabase _database;

        private SyncApiTestServer(
            WebApplication application,
            TestServerDatabase database,
            HttpClient client)
        {
            _application = application;
            _database = database;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<SyncApiTestServer> CreateAsync()
        {
            var database = await TestServerDatabase.CreateAsync();
            WebApplication? application = null;

            try
            {
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration[RequireSyncKeyFilter.ConfigurationName] = ReadKey;

                builder.Services.AddSingleton(database.Database);
                builder.Services.AddSingleton<ServerBackfillService>();
                builder.Services.AddDbContext<ServerRetailDbContext>(
                    options => options.UseSqlite(database.Database.ConnectionString));
                builder.Services.AddScoped<RetailLabDbContext>(
                    services => services.GetRequiredService<ServerRetailDbContext>());
                builder.Services.AddScoped<ISyncFeedReader, SyncFeedReader>();
                builder.Services.AddSingleton(TimeProvider.System);

                application = builder.Build();
                application.MapRetailLabSyncEndpoints();

                using (var scope = application.Services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
                    await DatabaseInitializer.SeedIfEmptyAsync(context);
                }

                await application.Services.GetRequiredService<ServerBackfillService>().PrepareAsync();
                await application.StartAsync();

                var addresses = application.Services
                    .GetRequiredService<IServer>()
                    .Features
                    .Get<IServerAddressesFeature>()
                    ?.Addresses;
                var address = Assert.Single(Assert.IsAssignableFrom<ICollection<string>>(addresses));
                var client = new HttpClient { BaseAddress = new Uri(address) };
                return new SyncApiTestServer(application, database, client);
            }
            catch
            {
                if (application is not null)
                {
                    await application.DisposeAsync();
                }

                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<HttpResponseMessage> PairAsync(Guid clientInstanceId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/v1/pair")
            {
                Content = JsonContent.Create(new PairRequest(clientInstanceId, DeclaresEmpty: true))
            };
            return await SendAuthorizedAsync(request);
        }

        public async Task<PairResponse> GetPairingAsync()
        {
            using var response = await PairAsync(Guid.NewGuid());
            response.EnsureSuccessStatusCode();
            return Assert.IsType<PairResponse>(
                await response.Content.ReadFromJsonAsync<PairResponse>());
        }

        public async Task<HttpResponseMessage> SendAuthorizedAsync(HttpRequestMessage request)
        {
            request.Headers.Add(RequireSyncKeyFilter.HeaderName, ReadKey);
            return await Client.SendAsync(request);
        }

        public async Task UpdateFirstProductAsync()
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var product = await context.Products.OrderBy(item => item.Sku).FirstAsync();
            product.UpdateDetails("Changed through the HTTP endpoint test", product.Price);
            await context.SaveChangesAsync();
        }

        public async Task DeleteFeedEntryAsync(long sequence)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var entry = await context.SyncChangeFeedEntries.SingleAsync(
                item => item.Sequence == sequence);
            context.SyncChangeFeedEntries.Remove(entry);
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();
            await _database.DisposeAsync();
        }
    }
}
