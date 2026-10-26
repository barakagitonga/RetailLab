using System.Text.Json;
using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Owns the local outbox JSON representation. This keeps persistence concerns
/// out of Core while allowing an immutable operation to be reconstructed.
/// </summary>
public static class SyncOperationSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializePayload(ISyncProductOperation operation) => operation switch
    {
        CreateProductOperation create => JsonSerializer.Serialize(
            new CreatePayload(
                create.Sku,
                create.Description,
                create.Price,
                create.InitialStockQuantity),
            JsonOptions),
        UpdateProductDetailsOperation update => JsonSerializer.Serialize(
            new UpdatePayload(update.Description, update.Price),
            JsonOptions),
        AdjustStockOperation adjustment => JsonSerializer.Serialize(
            new AdjustStockPayload(adjustment.Delta, adjustment.Reason),
            JsonOptions),
        ArchiveProductOperation or UnarchiveProductOperation => "{}",
        _ => throw new ArgumentException("The synchronization operation kind is unsupported.", nameof(operation))
    };

    public static ISyncProductOperation Deserialize(SyncOutboxEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            return entry.Kind switch
            {
                SyncOperationKind.CreateProduct => Create(entry),
                SyncOperationKind.UpdateProductDetails => Update(entry),
                SyncOperationKind.ArchiveProduct => new ArchiveProductOperation(
                    entry.OperationId,
                    entry.ProductId,
                    entry.BaseServerRevision,
                    entry.PredecessorOperationId,
                    entry.CreatedAtUtc),
                SyncOperationKind.UnarchiveProduct => new UnarchiveProductOperation(
                    entry.OperationId,
                    entry.ProductId,
                    entry.BaseServerRevision,
                    entry.PredecessorOperationId,
                    entry.CreatedAtUtc),
                SyncOperationKind.AdjustStock => AdjustStock(entry),
                _ => throw new SyncLocalStateException("The outbox contains an unsupported operation kind.")
            };
        }
        catch (JsonException exception)
        {
            throw new SyncLocalStateException(
                $"Outbox operation {entry.OperationId:D} contains invalid local JSON.",
                exception);
        }
        catch (Exception exception) when (exception is ArgumentException or BusinessRuleException)
        {
            throw new SyncLocalStateException(
                $"Outbox operation {entry.OperationId:D} contains invalid local data.",
                exception);
        }
    }

    private static CreateProductOperation Create(SyncOutboxEntry entry)
    {
        var payload = JsonSerializer.Deserialize<CreatePayload>(entry.Payload, JsonOptions)
            ?? throw new JsonException("The create payload is empty.");
        return new CreateProductOperation(
            entry.OperationId,
            entry.ProductId,
            payload.Sku,
            payload.Description,
            payload.Price,
            payload.InitialStock,
            entry.CreatedAtUtc);
    }

    private static UpdateProductDetailsOperation Update(SyncOutboxEntry entry)
    {
        var payload = JsonSerializer.Deserialize<UpdatePayload>(entry.Payload, JsonOptions)
            ?? throw new JsonException("The update payload is empty.");
        return new UpdateProductDetailsOperation(
            entry.OperationId,
            entry.ProductId,
            payload.Description,
            payload.Price,
            entry.BaseServerRevision,
            entry.PredecessorOperationId,
            entry.CreatedAtUtc);
    }

    private static AdjustStockOperation AdjustStock(SyncOutboxEntry entry)
    {
        var payload = JsonSerializer.Deserialize<AdjustStockPayload>(entry.Payload, JsonOptions)
            ?? throw new JsonException("The stock-adjustment payload is empty.");
        return new AdjustStockOperation(
            entry.OperationId,
            entry.ProductId,
            payload.Delta,
            payload.Reason,
            entry.BaseServerRevision,
            entry.PredecessorOperationId,
            entry.CreatedAtUtc);
    }

    private sealed record CreatePayload(
        string Sku,
        string Description,
        decimal Price,
        int InitialStock);

    private sealed record UpdatePayload(string Description, decimal Price);

    private sealed record AdjustStockPayload(int Delta, string Reason);
}
