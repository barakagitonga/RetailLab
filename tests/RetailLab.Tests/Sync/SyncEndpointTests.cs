using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
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

    [Fact]
    public async Task UploadAuthentication_UsesOneUniformFailureForUnknownWrongAndRevokedDevices()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var enrolled = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);

        using var unknown = await server.SendDeviceUploadAsync(Guid.NewGuid(), "unknown-secret");
        using var wrong = await server.SendDeviceUploadAsync(enrolled.DeviceId, "wrong-secret");
        await server.RevokeDeviceAsync(enrolled.DeviceId);
        using var revoked = await server.SendDeviceUploadAsync(enrolled.DeviceId, enrolled.Secret);

        foreach (var response in new[] { unknown, wrong, revoked })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("invalid_device_credential", await ReadErrorCodeAsync(response));
        }
    }

    [Fact]
    public async Task UploadAuthentication_SeparatesValidCredentialFromRoleAuthorization()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var staff = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var viewer = await server.EnrollUnauthorizedDeviceForDefenseTestAsync();

        using var staffResponse = await server.SendDeviceUploadAsync(staff.DeviceId, staff.Secret);
        using var viewerResponse = await server.SendDeviceUploadAsync(viewer.DeviceId, viewer.Secret);

        Assert.Equal(HttpStatusCode.BadRequest, staffResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);
        Assert.Equal("unauthorized_device", await ReadErrorCodeAsync(viewerResponse));
        Assert.NotNull(await server.GetLastSeenAsync(staff.DeviceId));
    }

    [Fact]
    public async Task Upload_CreateIsAtomicAndAnIdenticalRetryReplaysItsReceipt()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        var device = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var operationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var payload = CreateUploadPayload(
            pairing.ServerInstanceId,
            operationId,
            productId,
            "UPLOAD-001",
            "Uploaded product");

        using var firstResponse = await server.SendDeviceUploadAsync(device.DeviceId, device.Secret, payload);
        using var replayResponse = await server.SendDeviceUploadAsync(device.DeviceId, device.Secret, payload);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.False(await ReadReplayFlagAsync(firstResponse));
        Assert.True(await ReadReplayFlagAsync(replayResponse));

        var persisted = await server.GetUploadPersistenceAsync(operationId, productId);
        Assert.Equal(1, persisted.ProductRevision);
        Assert.Equal(device.DeviceId, persisted.ReceiptDeviceId);
        Assert.StartsWith("Device ", persisted.DeviceDisplayName);
        Assert.Equal("Endpoint tester", persisted.ActorDisplayName);
        Assert.Equal(SyncDeviceRoles.Staff, persisted.DeviceRole);
        Assert.False(string.IsNullOrWhiteSpace(persisted.OperationHash));
        Assert.Equal(1, persisted.ResultServerRevision);
        Assert.False(string.IsNullOrWhiteSpace(persisted.ResultSnapshot));
        Assert.Equal(1, persisted.ProductFeedEntries);
        Assert.Equal(1, persisted.Receipts);
    }

    [Fact]
    public async Task Upload_RejectsOperationIdReuseWithChangedPayloadOrAnotherDevice()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        var firstDevice = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var secondDevice = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var operationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var original = CreateUploadPayload(
            pairing.ServerInstanceId,
            operationId,
            productId,
            "UPLOAD-002",
            "Original payload");
        var changed = CreateUploadPayload(
            pairing.ServerInstanceId,
            operationId,
            productId,
            "UPLOAD-002",
            "Substituted payload");

        using var accepted = await server.SendDeviceUploadAsync(
            firstDevice.DeviceId,
            firstDevice.Secret,
            original);
        using var changedPayload = await server.SendDeviceUploadAsync(
            firstDevice.DeviceId,
            firstDevice.Secret,
            changed);
        using var anotherDevice = await server.SendDeviceUploadAsync(
            secondDevice.DeviceId,
            secondDevice.Secret,
            original);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, changedPayload.StatusCode);
        Assert.Equal("operation_id_payload_mismatch", await ReadErrorCodeAsync(changedPayload));
        Assert.Equal(HttpStatusCode.BadRequest, anotherDevice.StatusCode);
        Assert.Equal("operation_id_conflict", await ReadErrorCodeAsync(anotherDevice));
    }

    [Fact]
    public async Task Upload_CausalChainCrossesOperationsAndStaleArchiveWinsBeforeStateCheck()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        var device = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var productId = Guid.NewGuid();
        var createId = Guid.NewGuid();
        var updateId = Guid.NewGuid();
        var causalPayload = new
        {
            protocolVersion = SyncProtocolVersions.Upload,
            serverInstanceId = pairing.ServerInstanceId,
            operations = new object[]
            {
                new
                {
                    operationId = createId,
                    kind = "createProduct",
                    productId,
                    createdAtUtc = DateTimeOffset.UtcNow,
                    details = new { sku = "CHAIN-1", description = "Created", price = 10m, initialStock = 1 }
                },
                new
                {
                    operationId = updateId,
                    kind = "updateProductDetails",
                    productId,
                    createdAtUtc = DateTimeOffset.UtcNow,
                    predecessorOperationId = createId,
                    details = new { description = "Updated in chain", price = 11m }
                }
            }
        };

        using var causalResponse = await server.SendDeviceUploadAsync(
            device.DeviceId,
            device.Secret,
            causalPayload);
        Assert.Equal(HttpStatusCode.OK, causalResponse.StatusCode);
        using (var document = JsonDocument.Parse(await causalResponse.Content.ReadAsStringAsync()))
        {
            var results = document.RootElement.GetProperty("results");
            Assert.Equal("accepted", results[0].GetProperty("outcome").GetString());
            Assert.Equal(1, results[0].GetProperty("serverRevision").GetInt64());
            Assert.Equal("accepted", results[1].GetProperty("outcome").GetString());
            Assert.Equal(2, results[1].GetProperty("serverRevision").GetInt64());
        }

        var existingProductId = await server.GetFirstProductIdAsync();
        var archiveId = Guid.NewGuid();
        using var acceptedArchive = await server.SendDeviceUploadAsync(
            device.DeviceId,
            device.Secret,
            CreateArchivePayload(pairing.ServerInstanceId, archiveId, existingProductId, 1));
        Assert.Equal(HttpStatusCode.OK, acceptedArchive.StatusCode);

        using var staleArchive = await server.SendDeviceUploadAsync(
            device.DeviceId,
            device.Secret,
            CreateArchivePayload(
                pairing.ServerInstanceId,
                Guid.NewGuid(),
                existingProductId,
                1));
        using var staleDocument = JsonDocument.Parse(await staleArchive.Content.ReadAsStringAsync());
        var staleResult = staleDocument.RootElement.GetProperty("results")[0];
        Assert.Equal("conflict", staleResult.GetProperty("outcome").GetString());
        Assert.Equal("stale_base_revision", staleResult.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Upload_AdjustmentRebasesAndCommitsLedgerFeedReceiptExactlyOnce()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        var device = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var (productId, stock, revision) = await server.GetFirstProductStateAsync();
        await server.UpdateFirstProductAsync();
        var operationId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 10, 22, 7, 30, 0, TimeSpan.Zero);
        var payload = CreateAdjustmentPayload(
            pairing.ServerInstanceId,
            operationId,
            productId,
            revision,
            delta: 4,
            SynchronizedAdjustmentReasons.Restock,
            occurredAt);

        using var response = await server.SendDeviceUploadAsync(device.DeviceId, device.Secret, payload);
        using var replay = await server.SendDeviceUploadAsync(device.DeviceId, device.Secret, payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var result = document.RootElement.GetProperty("results")[0];
            Assert.Equal("accepted", result.GetProperty("outcome").GetString());
            Assert.Equal(revision + 2, result.GetProperty("serverRevision").GetInt64());
            Assert.Equal(stock + 4, result.GetProperty("snapshot").GetProperty("stock").GetInt32());
        }
        Assert.True(await ReadReplayFlagAsync(replay));

        var persisted = await server.GetAdjustmentPersistenceAsync(operationId, productId);
        Assert.Equal(stock + 4, persisted.Stock);
        Assert.Equal(revision + 2, persisted.Revision);
        Assert.Equal(1, persisted.Adjustments);
        Assert.Equal(1, persisted.Receipts);
        Assert.Equal(1, persisted.AdjustmentFeedEntries);
        Assert.Equal(4, persisted.QuantityChange);
        Assert.Equal(stock + 4, persisted.ResultingQuantity);
        Assert.Equal(SynchronizedAdjustmentReasons.Restock, persisted.Reason);
        Assert.Equal("Endpoint tester", persisted.Actor);
        Assert.Equal(occurredAt, persisted.CreatedAtUtc);
        using var recorded = JsonDocument.Parse(persisted.RecordedResult);
        var metadata = recorded.RootElement.GetProperty("adjustment");
        Assert.Equal(revision, metadata.GetProperty("resolvedBaseRevision").GetInt64());
        Assert.Equal(revision + 1, metadata.GetProperty("appliedAgainstRevision").GetInt64());
        Assert.True(metadata.GetProperty("rebased").GetBoolean());
    }

    [Fact]
    public async Task Upload_AdjustmentBelowZeroConflictsWithoutMutationLedgerOrFeed()
    {
        await using var server = await SyncApiTestServer.CreateAsync();
        var pairing = await server.GetPairingAsync();
        var device = await server.EnrollDeviceAsync(SyncDeviceRoles.Staff);
        var (productId, stock, revision) = await server.GetFirstProductStateAsync();
        var operationId = Guid.NewGuid();
        var payload = CreateAdjustmentPayload(
            pairing.ServerInstanceId,
            operationId,
            productId,
            revision,
            delta: -(stock + 1),
            SynchronizedAdjustmentReasons.Damage,
            DateTimeOffset.UtcNow);

        using var response = await server.SendDeviceUploadAsync(device.DeviceId, device.Secret, payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var result = document.RootElement.GetProperty("results")[0];
            Assert.Equal("conflict", result.GetProperty("outcome").GetString());
            Assert.Equal("insufficient_stock", result.GetProperty("code").GetString());
            Assert.Equal(stock, result.GetProperty("snapshot").GetProperty("stock").GetInt32());
        }
        var persisted = await server.GetAdjustmentPersistenceAsync(operationId, productId);
        Assert.Equal(stock, persisted.Stock);
        Assert.Equal(revision, persisted.Revision);
        Assert.Equal(0, persisted.Adjustments);
        Assert.Equal(1, persisted.Receipts);
        Assert.Equal(0, persisted.AdjustmentFeedEntries);
    }

    private static object CreateUploadPayload(
        Guid serverInstanceId,
        Guid operationId,
        Guid productId,
        string sku,
        string description) =>
        new
        {
            protocolVersion = SyncProtocolVersions.Upload,
            serverInstanceId,
            operations = new[]
            {
                new
                {
                    operationId,
                    kind = "createProduct",
                    productId,
                    createdAtUtc = DateTimeOffset.UtcNow,
                    details = new
                    {
                        sku,
                        description,
                        price = 12.50m,
                        initialStock = 3
                    }
                }
            }
        };

    private static object CreateArchivePayload(
        Guid serverInstanceId,
        Guid operationId,
        Guid productId,
        long baseRevision) =>
        new
        {
            protocolVersion = SyncProtocolVersions.Upload,
            serverInstanceId,
            operations = new[]
            {
                new
                {
                    operationId,
                    kind = "archiveProduct",
                    productId,
                    createdAtUtc = DateTimeOffset.UtcNow,
                    baseServerRevision = baseRevision,
                    details = new { }
                }
            }
        };

    private static object CreateAdjustmentPayload(
        Guid serverInstanceId,
        Guid operationId,
        Guid productId,
        long baseRevision,
        int delta,
        string reason,
        DateTimeOffset createdAtUtc) =>
        new
        {
            protocolVersion = SyncProtocolVersions.Upload,
            serverInstanceId,
            operations = new[]
            {
                new
                {
                    operationId,
                    kind = "adjustStock",
                    productId,
                    createdAtUtc,
                    baseServerRevision = baseRevision,
                    details = new { delta, reason }
                }
            }
        };

    private static async Task<bool> ReadReplayFlagAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("results")[0].GetProperty("replay").GetBoolean();
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
                builder.Services.AddScoped<IPasswordHasher<SyncDevice>, PasswordHasher<SyncDevice>>();
                builder.Services.AddScoped<SyncDeviceCredentialService>();
                builder.Services.AddScoped<ServerSyncOperationProcessor>();

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

        public async Task<(Guid DeviceId, string Secret)> EnrollDeviceAsync(string role)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var credentials = scope.ServiceProvider.GetRequiredService<SyncDeviceCredentialService>();
            var device = new SyncDevice(
                Guid.NewGuid(),
                $"Device {Guid.NewGuid():N}",
                "Endpoint tester",
                role,
                DateTimeOffset.UtcNow);
            var secret = credentials.IssueNewSecret(device);
            context.SyncDevices.Add(device);
            await context.SaveChangesAsync();
            return (device.Id, secret);
        }

        public async Task<(Guid DeviceId, string Secret)> EnrollUnauthorizedDeviceForDefenseTestAsync()
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var credentials = scope.ServiceProvider.GetRequiredService<SyncDeviceCredentialService>();
            var device = new SyncDevice(
                Guid.NewGuid(),
                $"Device {Guid.NewGuid():N}",
                "Authorization defense test",
                "viewer",
                DateTimeOffset.UtcNow);
            var secret = credentials.IssueNewSecret(device);

            // The production schema permits only staff. This test deliberately
            // bypasses the SQLite check to prove the HTTP authorization layer
            // still fails closed if persisted data is ever corrupted.
            await context.Database.OpenConnectionAsync();
            try
            {
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = ON;");
                context.SyncDevices.Add(device);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = OFF;");
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
            return (device.Id, secret);
        }

        public async Task RevokeDeviceAsync(Guid deviceId)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var device = await context.SyncDevices.SingleAsync(item => item.Id == deviceId);
            device.Revoke(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        public async Task<DateTimeOffset?> GetLastSeenAsync(Guid deviceId)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            return await context.SyncDevices
                .Where(item => item.Id == deviceId)
                .Select(item => item.LastSeenAtUtc)
                .SingleAsync();
        }

        public Task<HttpResponseMessage> SendDeviceUploadAsync(Guid deviceId, string secret) =>
            SendDeviceUploadAsync(deviceId, secret, new { });

        public Task<HttpResponseMessage> SendDeviceUploadAsync(
            Guid deviceId,
            string secret,
            object payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync/v1/upload")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add(RequireDeviceCredentialFilter.DeviceIdHeaderName, deviceId.ToString("D"));
            request.Headers.Add(RequireDeviceCredentialFilter.DeviceKeyHeaderName, secret);
            return Client.SendAsync(request);
        }

        public async Task<(
            long ProductRevision,
            Guid? ReceiptDeviceId,
            string? DeviceDisplayName,
            string? ActorDisplayName,
            string? DeviceRole,
            string? OperationHash,
            long? ResultServerRevision,
            string? ResultSnapshot,
            int ProductFeedEntries,
            int Receipts)> GetUploadPersistenceAsync(Guid operationId, Guid productId)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var productRevision = await context.Products
                .Where(item => item.Id == productId)
                .Select(item => item.ServerRevision)
                .SingleAsync();
            var receipt = await context.SyncInboxReceipts
                .SingleAsync(item => item.OperationId == operationId);
            var productFeedEntries = await context.SyncChangeFeedEntries
                .CountAsync(item => item.ProductId == productId);
            var receipts = await context.SyncInboxReceipts
                .CountAsync(item => item.OperationId == operationId);
            return (
                productRevision,
                receipt.DeviceId,
                receipt.DeviceDisplayName,
                receipt.ActorDisplayName,
                receipt.DeviceRole,
                receipt.OperationHash,
                receipt.ResultServerRevision,
                receipt.ResultSnapshot,
                productFeedEntries,
                receipts);
        }

        public async Task<Guid> GetFirstProductIdAsync()
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            return await context.Products
                .OrderBy(product => product.Sku)
                .Select(product => product.Id)
                .FirstAsync();
        }

        public async Task<(Guid ProductId, int Stock, long Revision)> GetFirstProductStateAsync()
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            return await context.Products
                .OrderBy(product => product.Sku)
                .Select(product => new ValueTuple<Guid, int, long>(
                    product.Id,
                    product.StockQuantity,
                    product.ServerRevision))
                .FirstAsync();
        }

        public async Task<(
            int Stock,
            long Revision,
            int Adjustments,
            int Receipts,
            int AdjustmentFeedEntries,
            int? QuantityChange,
            int? ResultingQuantity,
            string? Reason,
            string? Actor,
            DateTimeOffset? CreatedAtUtc,
            string RecordedResult)> GetAdjustmentPersistenceAsync(
                Guid operationId,
                Guid productId)
        {
            using var scope = _application.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ServerRetailDbContext>();
            var product = await context.Products
                .AsNoTracking()
                .SingleAsync(item => item.Id == productId);
            var adjustments = await context.InventoryAdjustments
                .AsNoTracking()
                .Where(item => item.ProductId == productId)
                .ToListAsync();
            adjustments = adjustments
                .Where(item => item.CreatedAtUtc >= new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero))
                .ToList();
            var receipt = await context.SyncInboxReceipts
                .AsNoTracking()
                .SingleAsync(item => item.OperationId == operationId);
            var adjustment = adjustments.SingleOrDefault();
            var adjustmentFeedEntries = await context.SyncChangeFeedEntries
                .CountAsync(item =>
                    item.ProductId == productId &&
                    receipt.ResultServerRevision.HasValue &&
                    item.ServerRevision == receipt.ResultServerRevision.Value);
            var receiptCount = await context.SyncInboxReceipts
                .CountAsync(item => item.OperationId == operationId);
            return (
                product.StockQuantity,
                product.ServerRevision,
                adjustments.Count,
                receiptCount,
                adjustmentFeedEntries,
                adjustment?.QuantityChange,
                adjustment?.ResultingQuantity,
                adjustment?.Reason,
                adjustment?.ActorIdentifier,
                adjustment?.CreatedAtUtc,
                Assert.IsType<string>(receipt.RecordedResult));
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
