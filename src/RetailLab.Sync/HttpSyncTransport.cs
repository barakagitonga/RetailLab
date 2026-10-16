using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RetailLab.Core;

namespace RetailLab.Sync;

public sealed class HttpSyncTransport : ISyncServerTransport
{
    public const string ReadKeyEnvironmentVariable = "RETAILLAB_SYNC_READ_KEY";
    public const string ReadKeyHeaderName = "X-RetailLab-Sync-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    private readonly HttpClient _httpClient;
    private readonly SyncClientOptions _options;
    private readonly string _readKey;

    public HttpSyncTransport(HttpClient httpClient, SyncClientOptions options, string? readKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(readKey))
        {
            throw new SyncConfigurationException(
                $"Set the {ReadKeyEnvironmentVariable} environment variable before synchronizing.");
        }

        _httpClient = httpClient;
        _options = options;
        _readKey = readKey;
    }

    public async Task<PairResponse> PairAsync(
        PairRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.ServerBaseUrl, "api/sync/v1/pair"))
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        using var response = await SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowMappedErrorAsync(response, cursor: null, cancellationToken);
        }

        var result = await ReadJsonAsync<PairResponse>(response, cancellationToken);
        if (result.ServerInstanceId == Guid.Empty ||
            result.ServerTimeUtc == default ||
            result.HeadSequence < 0)
        {
            throw new SyncProtocolException("The pairing response contains invalid required values.");
        }

        return result;
    }

    public async Task<PullResponse> PullAsync(
        PullRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"api/sync/v1/pull?clientInstanceId={request.ClientInstanceId:D}&serverInstanceId={request.ServerInstanceId:D}&after={request.After}&limit={request.Limit}");
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.ServerBaseUrl, query));

        using var response = await SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowMappedErrorAsync(response, request.After, cancellationToken);
        }

        var result = await ReadJsonAsync<PullResponse>(response, cancellationToken);
        ValidatePullResponse(result);
        return result;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Add(ReadKeyHeaderName, _readKey);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.Timeout);

        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SyncTimeoutException(_options.Timeout, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SyncServerUnavailableException(_options.ServerBaseUrl, exception);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                ?? throw new SyncProtocolException("The server returned an empty JSON response.");
        }
        catch (JsonException exception)
        {
            var location = exception.Path is null ? string.Empty : $" at {exception.Path}";
            throw new SyncProtocolException(
                $"The server returned data this client does not understand{location}.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new SyncProtocolException(
                "The server returned data this client does not understand.",
                exception);
        }
    }

    private static void ValidatePullResponse(PullResponse response)
    {
        if (response.Entries is null || response.HeadSequence < 0)
        {
            throw new SyncProtocolException("The pull response contains invalid paging metadata.");
        }

        foreach (var entry in response.Entries)
        {
            if (entry is null ||
                entry.Sequence <= 0 ||
                entry.ProductId == Guid.Empty ||
                entry.ServerRevision <= 0 ||
                entry.Snapshot is null ||
                entry.OccurredAtUtc == default)
            {
                throw new SyncProtocolException("The pull response contains an invalid feed entry.");
            }
        }
    }

    private static async Task ThrowMappedErrorAsync(
        HttpResponseMessage response,
        long? cursor,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new SyncAuthenticationException();
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            if (error?.Code == "client_not_empty")
            {
                throw new DatabaseNotEligibleForPairingException(0, 0);
            }

            throw new SyncProtocolException(
                error?.Code is null
                    ? "The server rejected the synchronization request."
                    : $"The server rejected the synchronization request ({error.Code}).");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            switch (error?.Code)
            {
                case "server_identity_changed":
                    throw new ServerIdentityChangedException();
                case "cursor_beyond_head":
                    throw new CursorBeyondHeadException(cursor ?? 0, error.HeadSequence);
                case "feed_gap":
                    throw new SyncFeedGapException();
            }
        }

        throw new SyncServerResponseException((int)response.StatusCode, error?.Code);
    }

    private static async Task<SyncApiError?> TryReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<SyncApiError>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SyncApiError(string Code, string Message, long? HeadSequence = null);
}
