using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Web.Sync;

public static class SyncEndpoints
{
    private const int DefaultPageSize = 100;
    private const int MaximumPageSize = 500;

    public static IEndpointRouteBuilder MapRetailLabSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/sync/v1/pair", PairAsync)
            .AddEndpointFilter<RequireSyncKeyFilter>();

        endpoints.MapGet("/api/sync/v1/pull", PullAsync)
            .AddEndpointFilter<RequireSyncKeyFilter>();

        return endpoints;
    }

    private static async Task<IResult> PairAsync(
        PairRequest request,
        ServerBackfillService serverPreparation,
        ISyncFeedReader feedReader,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.ClientInstanceId == Guid.Empty)
        {
            return Results.BadRequest(new SyncApiError("invalid_client", "Client instance ID is required."));
        }

        if (!request.DeclaresEmpty)
        {
            return Results.BadRequest(
                new SyncApiError(
                    "client_not_empty",
                    "Tutorial 7C pairing supports only empty sync-managed client databases."));
        }

        var response = new PairResponse(
            await serverPreparation.GetServerInstanceIdAsync(cancellationToken),
            timeProvider.GetUtcNow(),
            await feedReader.GetHeadSequenceAsync(cancellationToken));

        return Results.Ok(response);
    }

    private static async Task<IResult> PullAsync(
        Guid clientInstanceId,
        Guid serverInstanceId,
        long after,
        int? limit,
        ServerBackfillService serverPreparation,
        ISyncFeedReader feedReader,
        CancellationToken cancellationToken)
    {
        if (clientInstanceId == Guid.Empty)
        {
            return Results.BadRequest(new SyncApiError("invalid_client", "Client instance ID is required."));
        }

        if (after < 0)
        {
            return Results.BadRequest(new SyncApiError("invalid_cursor", "The feed cursor cannot be negative."));
        }

        var requestedLimit = limit ?? DefaultPageSize;
        if (requestedLimit is <= 0 or > MaximumPageSize)
        {
            return Results.BadRequest(
                new SyncApiError(
                    "invalid_limit",
                    $"The page limit must be between 1 and {MaximumPageSize}."));
        }

        var actualServerInstanceId = await serverPreparation.GetServerInstanceIdAsync(cancellationToken);
        if (serverInstanceId == Guid.Empty || serverInstanceId != actualServerInstanceId)
        {
            return Results.Json(
                new SyncApiError("server_identity_changed", "The paired server identity does not match."),
                statusCode: StatusCodes.Status409Conflict);
        }

        var response = await feedReader.ReadAfterAsync(after, requestedLimit, cancellationToken);
        if (after > response.HeadSequence)
        {
            return Results.Json(
                new SyncApiError(
                    "cursor_beyond_head",
                    "The supplied cursor is beyond the current feed head.",
                    response.HeadSequence),
                statusCode: StatusCodes.Status409Conflict);
        }

        var containsGap = response.Entries
            .Select((entry, index) => entry.Sequence != after + index + 1)
            .Any(isUnexpected => isUnexpected);
        if (after < response.HeadSequence &&
            (response.Entries.Count == 0 || containsGap))
        {
            return Results.Json(
                new SyncApiError(
                    "feed_gap",
                    "The authoritative feed contains an unexpected sequence gap.",
                    response.HeadSequence),
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(response);
    }

    private sealed record SyncApiError(string Code, string Message, long? HeadSequence = null);
}
