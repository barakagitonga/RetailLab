using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Applies one already-parsed upload operation to the authoritative database.
/// Each public write owns exactly one transaction and one terminal receipt.
/// </summary>
public sealed class ServerSyncOperationProcessor(
    RetailLabSqliteDatabase database,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UploadOperationResult?> TryReplayAsync(
        Guid operationId,
        Guid deviceId,
        string operationHash,
        CancellationToken cancellationToken = default)
    {
        await using var context = database.CreateServerDbContext();
        var receipt = await context.SyncInboxReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
        return receipt is null ? null : ResolveReceipt(receipt, deviceId, operationHash);
    }

    public Task<UploadOperationResult> RecordRejectedAsync(
        Guid operationId,
        string code,
        string operationHash,
        SyncDeviceAttribution attribution,
        AdjustmentReceiptMetadata? adjustmentMetadata = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            operationId,
            operationHash,
            attribution,
            (_, _) => Task.FromResult(
                new OperationDecision(
                    new UploadOperationResult(
                        operationId,
                        SyncOperationOutcome.Rejected,
                        code,
                        resultingServerRevision: null,
                        snapshot: null,
                        replay: false),
                    Product: null,
                    adjustmentMetadata)),
            cancellationToken);

    public Task<UploadOperationResult> ProcessAsync(
        ISyncProductOperation operation,
        string operationHash,
        SyncDeviceAttribution attribution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteAsync(
            operation.OperationId,
            operationHash,
            attribution,
            (context, token) => DecideAsync(context, operation, attribution, token),
            cancellationToken);
    }

    private async Task<UploadOperationResult> ExecuteAsync(
        Guid operationId,
        string operationHash,
        SyncDeviceAttribution attribution,
        Func<ServerRetailDbContext, CancellationToken, Task<OperationDecision>> decide,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(attribution.DeviceRole, SyncDeviceRoles.Staff, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The device is not authorized for product uploads.");
        }

        await using var context = database.CreateServerDbContext();
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction;
        try
        {
            transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new SyncUploadTransientException(exception);
        }
        await using (transaction)
        {
            try
            {
                var existing = await context.SyncInboxReceipts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
                if (existing is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ResolveReceipt(existing, attribution.DeviceId, operationHash);
                }

                var decision = await decide(context, cancellationToken);
                var result = decision.Result;
                var recordedResult = SerializeRecordedResult(result, decision.AdjustmentMetadata);
                var snapshotJson = result.Snapshot is null
                    ? null
                    : JsonSerializer.Serialize(result.Snapshot, JsonOptions);
                context.SyncInboxReceipts.Add(new SyncInboxReceipt(
                    operationId,
                    result.Outcome,
                    recordedResult,
                    timeProvider.GetUtcNow(),
                    attribution.DeviceId,
                    attribution.DeviceDisplayName,
                    attribution.ActorDisplayName,
                    attribution.DeviceRole,
                    operationHash,
                    result.ResultingServerRevision,
                    snapshotJson));

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (SyncOperationIdConflictException)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            catch (Exception exception) when (
                exception is DbUpdateException or SqliteException or InvalidOperationException or OverflowException)
            {
                await transaction.RollbackAsync(CancellationToken.None);

                await using var replayContext = database.CreateServerDbContext();
                var winner = await replayContext.SyncInboxReceipts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.OperationId == operationId, CancellationToken.None);
                if (winner is not null)
                {
                    return ResolveReceipt(winner, attribution.DeviceId, operationHash);
                }

                throw new SyncUploadTransientException(exception);
            }
        }
    }

    private async Task<OperationDecision> DecideAsync(
        ServerRetailDbContext context,
        ISyncProductOperation operation,
        SyncDeviceAttribution attribution,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            CreateProductOperation create => await DecideCreateAsync(context, create, cancellationToken),
            UpdateProductDetailsOperation update => await DecideUpdateAsync(
                context, update, attribution.DeviceId, cancellationToken),
            ArchiveProductOperation archive => await DecideArchiveAsync(
                context, archive, attribution.DeviceId, cancellationToken),
            UnarchiveProductOperation unarchive => await DecideUnarchiveAsync(
                context, unarchive, attribution.DeviceId, cancellationToken),
            AdjustStockOperation adjustment => await DecideAdjustmentAsync(
                context, adjustment, attribution, cancellationToken),
            _ => Rejected(operation.OperationId, "unsupported_operation")
        };
    }

    private static async Task<OperationDecision> DecideCreateAsync(
        ServerRetailDbContext context,
        CreateProductOperation operation,
        CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(item => item.Id == operation.ProductId, cancellationToken))
        {
            return Rejected(operation.OperationId, "duplicate_product_id");
        }

        var skuConflict = await context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Sku == operation.Sku, cancellationToken);
        if (skuConflict is not null)
        {
            return Conflict(operation.OperationId, "sku_conflict", ProductSnapshot.FromProduct(skuConflict));
        }

        var product = new Product(
            operation.ProductId,
            operation.Sku,
            operation.Description,
            operation.Price,
            operation.InitialStockQuantity);
        context.Products.Add(product);
        const long resultingRevision = 1;
        var snapshot = SnapshotAtRevision(product, resultingRevision);
        return new OperationDecision(
            new UploadOperationResult(
                operation.OperationId,
                SyncOperationOutcome.Accepted,
                code: null,
                resultingRevision,
                snapshot,
                replay: false),
            product);
    }

    private static async Task<OperationDecision> DecideUpdateAsync(
        ServerRetailDbContext context,
        UpdateProductDetailsOperation operation,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var product = await context.Products.SingleOrDefaultAsync(
            item => item.Id == operation.ProductId,
            cancellationToken);
        if (product is null)
        {
            return Rejected(operation.OperationId, "unknown_product");
        }

        var baseDecision = await ResolveBaseAsync(context, operation, deviceId, cancellationToken);
        if (baseDecision.ErrorCode is not null)
        {
            return Rejected(operation.OperationId, baseDecision.ErrorCode);
        }

        var revisionDecision = CompareRevision(operation.OperationId, baseDecision.Revision!.Value, product);
        if (revisionDecision is not null)
        {
            return revisionDecision;
        }

        if (product.IsArchived)
        {
            return Rejected(operation.OperationId, "product_archived");
        }

        product.UpdateDetails(operation.Description, operation.Price);
        return Accepted(operation.OperationId, product);
    }

    private static async Task<OperationDecision> DecideAdjustmentAsync(
        ServerRetailDbContext context,
        AdjustStockOperation operation,
        SyncDeviceAttribution attribution,
        CancellationToken cancellationToken)
    {
        var metadata = NewAdjustmentMetadata(operation);
        var product = await context.Products.SingleOrDefaultAsync(
            item => item.Id == operation.ProductId,
            cancellationToken);
        if (product is null)
        {
            return Rejected(operation.OperationId, "unknown_product", metadata);
        }

        metadata = metadata with { AppliedAgainstRevision = product.ServerRevision };
        var baseDecision = await ResolveBaseAsync(
            context,
            operation,
            attribution.DeviceId,
            cancellationToken);
        if (baseDecision.ErrorCode is not null)
        {
            return Rejected(operation.OperationId, baseDecision.ErrorCode, metadata);
        }

        var resolvedBase = baseDecision.Revision!.Value;
        metadata = metadata with { ResolvedBaseRevision = resolvedBase };
        if (resolvedBase > product.ServerRevision)
        {
            return Rejected(operation.OperationId, "invalid_base_revision", metadata);
        }

        if (product.IsArchived)
        {
            return Rejected(operation.OperationId, "product_archived", metadata);
        }

        var resultingQuantity = (long)product.StockQuantity + operation.Delta;
        if (resultingQuantity < 0)
        {
            return Conflict(
                operation.OperationId,
                "insufficient_stock",
                ProductSnapshot.FromProduct(product),
                metadata);
        }

        if (resultingQuantity > int.MaxValue)
        {
            return Rejected(operation.OperationId, "invalid_adjustment", metadata);
        }

        product.AdjustStock(operation.Delta);
        context.InventoryAdjustments.Add(new InventoryAdjustment(
            product,
            operation.Delta,
            product.StockQuantity,
            operation.Reason,
            attribution.ActorDisplayName,
            operation.CreatedAtUtc));
        var resultingRevision = checked(product.ServerRevision + 1);
        var snapshot = SnapshotAtRevision(product, resultingRevision);
        metadata = metadata with { Rebased = resolvedBase != metadata.AppliedAgainstRevision };
        return new OperationDecision(
            new UploadOperationResult(
                operation.OperationId,
                SyncOperationOutcome.Accepted,
                code: null,
                resultingRevision,
                snapshot,
                replay: false),
            product,
            metadata);
    }

    private async Task<OperationDecision> DecideArchiveAsync(
        ServerRetailDbContext context,
        ArchiveProductOperation operation,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var product = await context.Products.SingleOrDefaultAsync(
            item => item.Id == operation.ProductId,
            cancellationToken);
        if (product is null)
        {
            return Rejected(operation.OperationId, "unknown_product");
        }

        var baseDecision = await ResolveBaseAsync(context, operation, deviceId, cancellationToken);
        if (baseDecision.ErrorCode is not null)
        {
            return Rejected(operation.OperationId, baseDecision.ErrorCode);
        }

        var revisionDecision = CompareRevision(operation.OperationId, baseDecision.Revision!.Value, product);
        if (revisionDecision is not null)
        {
            return revisionDecision;
        }

        if (product.IsArchived)
        {
            return Rejected(operation.OperationId, "already_archived");
        }

        product.Archive(timeProvider.GetUtcNow());
        return Accepted(operation.OperationId, product);
    }

    private static async Task<OperationDecision> DecideUnarchiveAsync(
        ServerRetailDbContext context,
        UnarchiveProductOperation operation,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var product = await context.Products.SingleOrDefaultAsync(
            item => item.Id == operation.ProductId,
            cancellationToken);
        if (product is null)
        {
            return Rejected(operation.OperationId, "unknown_product");
        }

        var baseDecision = await ResolveBaseAsync(context, operation, deviceId, cancellationToken);
        if (baseDecision.ErrorCode is not null)
        {
            return Rejected(operation.OperationId, baseDecision.ErrorCode);
        }

        var revisionDecision = CompareRevision(operation.OperationId, baseDecision.Revision!.Value, product);
        if (revisionDecision is not null)
        {
            return revisionDecision;
        }

        if (!product.IsArchived)
        {
            return Rejected(operation.OperationId, "already_active");
        }

        product.Unarchive();
        return Accepted(operation.OperationId, product);
    }

    private static async Task<BaseResolution> ResolveBaseAsync(
        ServerRetailDbContext context,
        ICausalOperation operation,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (operation.BaseServerRevision.HasValue)
        {
            return new BaseResolution(operation.BaseServerRevision.Value, null);
        }

        var predecessor = await context.SyncInboxReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OperationId == operation.PredecessorOperationId,
                cancellationToken);
        if (predecessor is null)
        {
            return new BaseResolution(null, "unknown_predecessor");
        }

        if (predecessor.DeviceId != deviceId ||
            predecessor.Outcome != SyncOperationOutcome.Accepted ||
            !predecessor.ResultServerRevision.HasValue)
        {
            return new BaseResolution(null, "predecessor_not_accepted");
        }

        return new BaseResolution(predecessor.ResultServerRevision.Value, null);
    }

    private static OperationDecision? CompareRevision(
        Guid operationId,
        long baseRevision,
        Product product)
    {
        if (baseRevision == product.ServerRevision)
        {
            return null;
        }

        return baseRevision > product.ServerRevision
            ? Rejected(operationId, "invalid_base_revision")
            : Conflict(
                operationId,
                "stale_base_revision",
                ProductSnapshot.FromProduct(product));
    }

    private static OperationDecision Accepted(Guid operationId, Product product)
    {
        var revision = checked(product.ServerRevision + 1);
        return new OperationDecision(
            new UploadOperationResult(
                operationId,
                SyncOperationOutcome.Accepted,
                code: null,
                revision,
                snapshot: null,
                replay: false),
            product);
    }

    private static OperationDecision Rejected(
        Guid operationId,
        string code,
        AdjustmentReceiptMetadata? adjustmentMetadata = null) =>
        new(
            new UploadOperationResult(
                operationId,
                SyncOperationOutcome.Rejected,
                code,
                resultingServerRevision: null,
                snapshot: null,
                replay: false),
            Product: null,
            adjustmentMetadata);

    private static OperationDecision Conflict(
        Guid operationId,
        string code,
        ProductSnapshot snapshot,
        AdjustmentReceiptMetadata? adjustmentMetadata = null) =>
        new(
            new UploadOperationResult(
                operationId,
                SyncOperationOutcome.Conflict,
                code,
                resultingServerRevision: null,
                snapshot,
                replay: false),
            Product: null,
            adjustmentMetadata);

    private static AdjustmentReceiptMetadata NewAdjustmentMetadata(
        AdjustStockOperation operation) => new(
            "value",
            operation.Delta,
            "value",
            operation.Reason,
            operation.BaseServerRevision,
            AppliedAgainstRevision: null,
            Rebased: null);

    private static string SerializeRecordedResult(
        UploadOperationResult result,
        AdjustmentReceiptMetadata? adjustmentMetadata)
    {
        if (adjustmentMetadata is null)
        {
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        var root = JsonSerializer.SerializeToNode(result, JsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("The upload result could not be serialized.");
        root["adjustment"] = JsonSerializer.SerializeToNode(adjustmentMetadata, JsonOptions);
        return root.ToJsonString(JsonOptions);
    }

    private static UploadOperationResult ResolveReceipt(
        SyncInboxReceipt receipt,
        Guid deviceId,
        string operationHash)
    {
        if (!receipt.DeviceId.HasValue || string.IsNullOrWhiteSpace(receipt.OperationHash))
        {
            throw new SyncOperationIdConflictException("operation_id_conflict");
        }

        if (receipt.DeviceId.Value != deviceId)
        {
            throw new SyncOperationIdConflictException("operation_id_conflict");
        }

        if (!string.Equals(receipt.OperationHash, operationHash, StringComparison.Ordinal))
        {
            throw new SyncOperationIdConflictException("operation_id_payload_mismatch");
        }

        if (string.IsNullOrWhiteSpace(receipt.RecordedResult))
        {
            throw new SyncOperationIdConflictException("operation_id_conflict");
        }

        var result = JsonSerializer.Deserialize<UploadOperationResult>(receipt.RecordedResult, JsonOptions)
            ?? throw new SyncOperationIdConflictException("operation_id_conflict");
        return result with { Replay = true };
    }

    private static ProductSnapshot SnapshotAtRevision(Product product, long revision) =>
        new(
            product.Id,
            product.Sku,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.IsArchived,
            product.ArchivedAtUtc,
            revision);

    private sealed record OperationDecision(
        UploadOperationResult Result,
        Product? Product,
        AdjustmentReceiptMetadata? AdjustmentMetadata = null);

    private sealed record BaseResolution(long? Revision, string? ErrorCode);
}
