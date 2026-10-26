using System.Net;
using System.Text;
using System.Text.Json;
using RetailLab.Core;
using RetailLab.Sync;

namespace RetailLab.Tests.Sync;

public sealed class HttpSyncTransportTests
{
    private static readonly SyncClientOptions Options =
        new(new Uri("http://localhost:5197"));

    [Fact]
    public async Task PairAsync_SendsReadKeyAndUsesWebJsonNames()
    {
        var serverId = Guid.NewGuid();
        var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("retaillab-local-read-test", request.Headers.GetValues("X-RetailLab-Sync-Key").Single());
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"clientInstanceId\"", body);
            Assert.Contains("\"declaresEmpty\":true", body);
            return JsonResponse(
                HttpStatusCode.OK,
                $"{{\"serverInstanceId\":\"{serverId:D}\",\"serverTimeUtc\":\"2026-10-16T11:00:00Z\",\"headSequence\":18}}");
        });
        var transport = CreateTransport(handler);

        var response = await transport.PairAsync(new PairRequest(Guid.NewGuid(), true));

        Assert.Equal(serverId, response.ServerInstanceId);
        Assert.Equal(18, response.HeadSequence);
    }

    [Fact]
    public async Task PairAsync_Unauthorized_ThrowsAuthenticationException()
    {
        var transport = CreateTransport(new StubHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.Unauthorized, "{}"))));

        await Assert.ThrowsAsync<SyncAuthenticationException>(
            () => transport.PairAsync(new PairRequest(Guid.NewGuid(), true)));
    }

    [Fact]
    public async Task PullAsync_ServerIdentityConflict_ThrowsSpecificException()
    {
        var transport = CreateTransport(new StubHandler(_ => Task.FromResult(
            JsonResponse(
                HttpStatusCode.Conflict,
                "{\"code\":\"server_identity_changed\",\"message\":\"Mismatch\",\"headSequence\":null}"))));

        await Assert.ThrowsAsync<ServerIdentityChangedException>(() => transport.PullAsync(
            new PullRequest(Guid.NewGuid(), Guid.NewGuid(), 4, 100)));
    }

    [Fact]
    public async Task PullAsync_CursorConflict_PreservesRequestedAndServerCursorFacts()
    {
        var transport = CreateTransport(new StubHandler(_ => Task.FromResult(
            JsonResponse(
                HttpStatusCode.Conflict,
                "{\"code\":\"cursor_beyond_head\",\"message\":\"Ahead\",\"headSequence\":18}"))));

        var exception = await Assert.ThrowsAsync<CursorBeyondHeadException>(() => transport.PullAsync(
            new PullRequest(Guid.NewGuid(), Guid.NewGuid(), 19, 100)));

        Assert.Equal(19, exception.RequestedCursor);
        Assert.Equal(18, exception.HeadSequence);
    }

    [Fact]
    public async Task PullAsync_MissingRequiredJsonProperty_ThrowsProtocolException()
    {
        var transport = CreateTransport(new StubHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.OK, "{\"headSequence\":0,\"hasMore\":false}"))));

        await Assert.ThrowsAsync<SyncProtocolException>(() => transport.PullAsync(
            new PullRequest(Guid.NewGuid(), Guid.NewGuid(), 0, 100)));
    }

    [Fact]
    public async Task UploadAsync_UsesDeviceHeadersOnlyAndMatchesTheVersionOneWireShape()
    {
        var deviceId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var operation = new CreateProductOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WIRE-1",
            "Golden wire product",
            24.950m,
            10,
            new DateTimeOffset(2026, 10, 21, 9, 14, 3, TimeSpan.Zero));
        var handler = new StubHandler(async request =>
        {
            Assert.Equal(deviceId.ToString("D"), request.Headers.GetValues("X-RetailLab-Device-Id").Single());
            Assert.Equal("device-secret", request.Headers.GetValues("X-RetailLab-Device-Key").Single());
            Assert.False(request.Headers.Contains("X-RetailLab-Sync-Key"));

            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.False(body.RootElement.TryGetProperty("clientInstanceId", out _));
            var wireOperation = body.RootElement.GetProperty("operations")[0];
            Assert.Equal(JsonValueKind.Null, wireOperation.GetProperty("baseServerRevision").ValueKind);
            Assert.Equal(JsonValueKind.Null, wireOperation.GetProperty("predecessorOperationId").ValueKind);
            Assert.Equal("createProduct", wireOperation.GetProperty("kind").GetString());
            Assert.Equal(24.950m, wireOperation.GetProperty("details").GetProperty("price").GetDecimal());

            return JsonResponse(
                HttpStatusCode.OK,
                $$"""
                {"protocolVersion":1,"serverInstanceId":"{{serverId:D}}","serverTimeUtc":"2026-10-21T12:02:11Z","results":[{"operationId":"{{operation.OperationId:D}}","outcome":"accepted","code":null,"serverRevision":1,"replay":false,"snapshot":null}]}
                """);
        });
        var transport = CreateTransport(handler, "device-secret");

        var response = await transport.UploadAsync(
            new UploadRequest(deviceId, serverId, [operation]));

        Assert.Equal(1, Assert.Single(response.Results).ResultingServerRevision);
    }

    [Fact]
    public async Task UploadAsync_WithoutDeviceKeyStopsBeforeHttp()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP was not expected."));
        var transport = CreateTransport(handler);

        await Assert.ThrowsAsync<SyncConfigurationException>(() => transport.UploadAsync(
            new UploadRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [new CreateProductOperation(
                    Guid.NewGuid(), Guid.NewGuid(), "KEY-1", "Missing key", 1m, 1, DateTimeOffset.UtcNow)])));
    }

    [Fact]
    public async Task UploadAsync_AdjustmentUsesSignedDeltaAndReasonWireFields()
    {
        var deviceId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var operation = AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(),
            productId,
            -3,
            SynchronizedAdjustmentReasons.Damage,
            4,
            new DateTimeOffset(2026, 10, 22, 8, 0, 0, TimeSpan.Zero));
        var handler = new StubHandler(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var wire = body.RootElement.GetProperty("operations")[0];
            Assert.Equal("adjustStock", wire.GetProperty("kind").GetString());
            Assert.Equal(4, wire.GetProperty("baseServerRevision").GetInt64());
            Assert.Equal(-3, wire.GetProperty("details").GetProperty("delta").GetInt32());
            Assert.Equal("damage", wire.GetProperty("details").GetProperty("reason").GetString());
            return JsonResponse(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    protocolVersion = 1,
                    serverInstanceId = serverId,
                    serverTimeUtc = "2026-10-22T08:01:00Z",
                    results = new[]
                    {
                        new
                        {
                            operationId = operation.OperationId,
                            outcome = "conflict",
                            code = "insufficient_stock",
                            serverRevision = (long?)null,
                            replay = false,
                            snapshot = new
                            {
                                productId,
                                serverRevision = 5,
                                sku = "WIRE-A",
                                description = "Adjustment product",
                                price = 10,
                                stock = 2,
                                isArchived = false,
                                archivedAtUtc = (DateTimeOffset?)null
                            }
                        }
                    }
                }));
        });
        var transport = CreateTransport(handler, "device-secret");

        var response = await transport.UploadAsync(new UploadRequest(deviceId, serverId, [operation]));

        Assert.Equal("insufficient_stock", Assert.Single(response.Results).Code);
    }

    [Fact]
    public void SyncAssembly_DoesNotReferenceData()
    {
        var references = typeof(SyncOrchestrator).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name == "RetailLab.Data");
    }

    private static HttpSyncTransport CreateTransport(HttpMessageHandler handler, string? deviceKey = null)
    {
        return new HttpSyncTransport(
            new HttpClient(handler),
            Options,
            "retaillab-local-read-test",
            deviceKey);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }
}
