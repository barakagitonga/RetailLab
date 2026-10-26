using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Web.Sync;

internal static class UploadEndpoint
{
    public const int MaximumOperationCount = 100;
    public const int MaximumBodyBytes = 256 * 1024;

    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        ServerBackfillService serverPreparation,
        ServerSyncOperationProcessor processor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var device = RequireDeviceCredentialFilter.GetAuthenticatedDevice(httpContext);
        var attribution = SyncDeviceAttribution.FromDevice(device);

        byte[] body;
        try
        {
            body = await ReadBoundedBodyAsync(httpContext.Request, cancellationToken);
        }
        catch (UploadBodyTooLargeException)
        {
            return Error(StatusCodes.Status413PayloadTooLarge, "batch_too_large", "The upload batch is too large.");
        }

        UploadWireEnvelope envelope;
        try
        {
            envelope = UploadWireParser.Parse(body);
        }
        catch (UploadWireException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }

        if (envelope.ProtocolVersion != SyncProtocolVersions.Upload)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "unsupported_protocol_version",
                "The upload protocol version is not supported.");
        }

        if (envelope.Operations.Count > MaximumOperationCount)
        {
            return Error(StatusCodes.Status413PayloadTooLarge, "batch_too_large", "The upload batch is too large.");
        }

        var serverInstanceId = await serverPreparation.GetServerInstanceIdAsync(cancellationToken);
        if (envelope.ServerInstanceId != serverInstanceId)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "server_identity_changed",
                "The paired server identity does not match.");
        }

        var results = new List<UploadOperationResult>(envelope.Operations.Count);
        foreach (var draft in envelope.Operations)
        {
            var fingerprint = draft.ComputeFingerprint();
            try
            {
                var replay = await processor.TryReplayAsync(
                    draft.OperationId,
                    device.Id,
                    fingerprint,
                    cancellationToken);
                if (replay is not null)
                {
                    results.Add(replay);
                    continue;
                }

                UploadOperationResult result;
                if (!draft.TryCreateCoreOperation(out var operation, out var rejectionCode))
                {
                    result = await processor.RecordRejectedAsync(
                        draft.OperationId,
                        rejectionCode ?? "invalid_details",
                        fingerprint,
                        attribution,
                        draft.Kind == SyncOperationKind.AdjustStock
                            ? draft.CreateAdjustmentMetadata(rejectionCode)
                            : null,
                        cancellationToken);
                }
                else
                {
                    result = await processor.ProcessAsync(
                        operation!,
                        fingerprint,
                        attribution,
                        cancellationToken);
                }

                results.Add(result);
            }
            catch (SyncOperationIdConflictException exception)
            {
                return Error(StatusCodes.Status400BadRequest, exception.Code, "The operation ID conflicts with an existing receipt.");
            }
            catch (SyncUploadTransientException)
            {
                // Do not return a partial result set. The client safely retries the
                // entire batch and durable receipts replay completed operations.
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        return Results.Ok(new
        {
            protocolVersion = SyncProtocolVersions.Upload,
            serverInstanceId,
            serverTimeUtc = timeProvider.GetUtcNow(),
            results = results.Select(result => new
            {
                operationId = result.OperationId,
                outcome = result.Outcome switch
                {
                    SyncOperationOutcome.Accepted => "accepted",
                    SyncOperationOutcome.Conflict => "conflict",
                    SyncOperationOutcome.Rejected => "rejected",
                    _ => throw new InvalidOperationException("Unknown upload outcome.")
                },
                code = result.Code,
                serverRevision = result.ResultingServerRevision,
                replay = result.Replay,
                snapshot = result.Snapshot is null ? null : new
                {
                    productId = result.Snapshot.Id,
                    serverRevision = result.Snapshot.ServerRevision,
                    sku = result.Snapshot.Sku,
                    description = result.Snapshot.Description,
                    price = result.Snapshot.Price,
                    stock = result.Snapshot.StockQuantity,
                    isArchived = result.Snapshot.IsArchived,
                    archivedAtUtc = result.Snapshot.ArchivedAtUtc
                }
            })
        });
    }

    private static async Task<byte[]> ReadBoundedBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximumBodyBytes)
        {
            throw new UploadBodyTooLargeException();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await request.Body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaximumBodyBytes)
            {
                throw new UploadBodyTooLargeException();
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new SyncApiError(code, message), statusCode: statusCode);

    private sealed class UploadBodyTooLargeException : Exception;
}
