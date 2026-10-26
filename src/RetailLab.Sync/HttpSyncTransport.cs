using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using RetailLab.Core;

namespace RetailLab.Sync;

public sealed class HttpSyncTransport : ISyncServerTransport
{
    public const string ReadKeyEnvironmentVariable = "RETAILLAB_SYNC_READ_KEY";
    public const string DeviceKeyEnvironmentVariable = "RETAILLAB_SYNC_DEVICE_KEY";
    public const string ReadKeyHeaderName = "X-RetailLab-Sync-Key";
    public const string DeviceIdHeaderName = "X-RetailLab-Device-Id";
    public const string DeviceKeyHeaderName = "X-RetailLab-Device-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    private readonly HttpClient _httpClient;
    private readonly SyncClientOptions _options;
    private readonly string _readKey;
    private readonly string? _deviceKey;

    public HttpSyncTransport(
        HttpClient httpClient,
        SyncClientOptions options,
        string? readKey,
        string? deviceKey = null)
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
        _deviceKey = string.IsNullOrWhiteSpace(deviceKey) ? null : deviceKey;
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

    public async Task<UploadResponse> UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_deviceKey is null)
        {
            throw new SyncConfigurationException(
                $"Set the {DeviceKeyEnvironmentVariable} environment variable before uploading local changes.");
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.ServerBaseUrl, "api/sync/v1/upload"))
        {
            Content = JsonContent.Create(CreateUploadBody(request), options: JsonOptions)
        };
        message.Headers.Add(DeviceIdHeaderName, request.ClientInstanceId.ToString("D"));
        message.Headers.Add(DeviceKeyHeaderName, _deviceKey);

        using var response = await SendAsync(message, cancellationToken, includeReadKey: false);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowMappedErrorAsync(response, cursor: null, cancellationToken, deviceUpload: true);
        }

        var wire = await ReadJsonAsync<UploadWireResponse>(response, cancellationToken);
        if (wire.ProtocolVersion != SyncProtocolVersions.Upload ||
            wire.ServerInstanceId == Guid.Empty ||
            wire.ServerTimeUtc == default ||
            wire.Results is null)
        {
            throw new SyncProtocolException("The upload response contains invalid required values.");
        }

        var results = wire.Results.Select(ToUploadResult).ToList();
        return new UploadResponse(
            wire.ProtocolVersion,
            wire.ServerInstanceId,
            wire.ServerTimeUtc,
            results);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool includeReadKey = true)
    {
        if (includeReadKey)
        {
            request.Headers.Add(ReadKeyHeaderName, _readKey);
        }
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
        CancellationToken cancellationToken,
        bool deviceUpload = false)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (deviceUpload)
            {
                throw new SyncDeviceAuthenticationException();
            }

            throw new SyncAuthenticationException();
        }

        if (deviceUpload && response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new SyncDeviceAuthorizationException();
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

    private static JsonObject CreateUploadBody(UploadRequest request) => new()
    {
        ["protocolVersion"] = SyncProtocolVersions.Upload,
        ["serverInstanceId"] = request.ServerInstanceId,
        ["operations"] = new JsonArray(request.Operations.Select(CreateWireOperation).ToArray())
    };

    private static JsonNode CreateWireOperation(ISyncProductOperation operation)
    {
        var result = new JsonObject
        {
            ["operationId"] = operation.OperationId,
            ["kind"] = operation.Kind switch
            {
                SyncOperationKind.CreateProduct => "createProduct",
                SyncOperationKind.UpdateProductDetails => "updateProductDetails",
                SyncOperationKind.ArchiveProduct => "archiveProduct",
                SyncOperationKind.UnarchiveProduct => "unarchiveProduct",
                SyncOperationKind.AdjustStock => "adjustStock",
                _ => throw new SyncProtocolException("The local outbox contains an unsupported operation kind.")
            },
            ["productId"] = operation.ProductId,
            ["createdAtUtc"] = operation.CreatedAtUtc,
            ["baseServerRevision"] = null,
            ["predecessorOperationId"] = null
        };

        if (operation is ICausalOperation causal)
        {
            if (causal.BaseServerRevision.HasValue)
            {
                result["baseServerRevision"] = causal.BaseServerRevision.Value;
            }
            else
            {
                result["predecessorOperationId"] = causal.PredecessorOperationId!.Value;
            }
        }

        result["details"] = operation switch
        {
            CreateProductOperation create => new JsonObject
            {
                ["sku"] = create.Sku,
                ["description"] = create.Description,
                ["price"] = create.Price,
                ["initialStock"] = create.InitialStockQuantity
            },
            UpdateProductDetailsOperation update => new JsonObject
            {
                ["description"] = update.Description,
                ["price"] = update.Price
            },
            AdjustStockOperation adjustment => new JsonObject
            {
                ["delta"] = adjustment.Delta,
                ["reason"] = adjustment.Reason
            },
            _ => new JsonObject()
        };

        return result;
    }

    private static UploadOperationResult ToUploadResult(UploadWireResult wire)
    {
        if (wire.OperationId == Guid.Empty)
        {
            throw new SyncProtocolException("The upload response contains an empty operation ID.");
        }

        var outcome = wire.Outcome switch
        {
            "accepted" => SyncOperationOutcome.Accepted,
            "conflict" => SyncOperationOutcome.Conflict,
            "rejected" => SyncOperationOutcome.Rejected,
            _ => throw new SyncProtocolException("The upload response contains an unknown outcome.")
        };

        try
        {
            return new UploadOperationResult(
                wire.OperationId,
                outcome,
                wire.Code,
                wire.ServerRevision,
                wire.Snapshot is null
                    ? null
                    : new ProductSnapshot(
                        wire.Snapshot.ProductId,
                        wire.Snapshot.Sku,
                        wire.Snapshot.Description,
                        wire.Snapshot.Price,
                        wire.Snapshot.Stock,
                        wire.Snapshot.IsArchived,
                        wire.Snapshot.ArchivedAtUtc,
                        wire.Snapshot.ServerRevision),
                wire.Replay);
        }
        catch (ArgumentException exception)
        {
            throw new SyncProtocolException("The upload response contains an invalid operation result.", exception);
        }
    }

    private sealed record UploadWireResponse(
        int ProtocolVersion,
        Guid ServerInstanceId,
        DateTimeOffset ServerTimeUtc,
        IReadOnlyList<UploadWireResult> Results);

    private sealed record UploadWireResult(
        Guid OperationId,
        string Outcome,
        string? Code,
        long? ServerRevision,
        bool Replay,
        UploadWireSnapshot? Snapshot);

    private sealed record UploadWireSnapshot(
        Guid ProductId,
        long ServerRevision,
        string Sku,
        string Description,
        decimal Price,
        int Stock,
        bool IsArchived,
        DateTimeOffset? ArchivedAtUtc);
}
