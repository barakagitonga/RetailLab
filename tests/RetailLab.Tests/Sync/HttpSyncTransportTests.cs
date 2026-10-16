using System.Net;
using System.Text;
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

    private static HttpSyncTransport CreateTransport(HttpMessageHandler handler)
    {
        return new HttpSyncTransport(
            new HttpClient(handler),
            Options,
            "retaillab-local-read-test");
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
