using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Web.Sync;

internal enum WireFieldState
{
    Missing,
    Null,
    Value
}

internal readonly record struct WireField<T>(WireFieldState State, T? Value)
{
    public static WireField<T> Missing() => new(WireFieldState.Missing, default);

    public static WireField<T> Null() => new(WireFieldState.Null, default);

    public static WireField<T> Present(T value) => new(WireFieldState.Value, value);
}

internal sealed record UploadWireEnvelope(
    int ProtocolVersion,
    Guid ServerInstanceId,
    IReadOnlyList<UploadWireOperationDraft> Operations);

internal sealed record UploadWireOperationDraft(
    Guid OperationId,
    SyncOperationKind Kind,
    string KindToken,
    Guid ProductId,
    DateTimeOffset CreatedAtUtc,
    WireField<long> BaseServerRevision,
    WireField<Guid> PredecessorOperationId,
    WireField<string> Sku,
    WireField<string> Description,
    WireField<decimal> Price,
    WireField<int> InitialStock,
    WireField<int> Delta,
    WireField<string> Reason)
{
    public string ComputeFingerprint()
    {
        using var stream = new MemoryStream();
        var formatVersion = Kind == SyncOperationKind.AdjustStock ? "7e-fp-v1" : "7d-fp-v1";
        WriteField(stream, "formatVersion", WireField<string>.Present(formatVersion), value => value);
        WriteField(stream, "operationId", WireField<Guid>.Present(OperationId), FormatGuid);
        WriteField(stream, "kind", WireField<string>.Present(KindToken), value => value);
        WriteField(stream, "productId", WireField<Guid>.Present(ProductId), FormatGuid);
        WriteField(
            stream,
            "createdAtUtc",
            WireField<DateTimeOffset>.Present(CreatedAtUtc),
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        WriteField(stream, "baseServerRevision", BaseServerRevision, FormatLong);
        WriteField(stream, "predecessorOperationId", PredecessorOperationId, FormatGuid);
        WriteField(stream, "sku", Sku, value => value);
        WriteField(stream, "description", Description, value => value);
        WriteField(stream, "price", Price, value => value.ToString("G29", CultureInfo.InvariantCulture));
        WriteField(stream, "initialStock", InitialStock, value => value.ToString(CultureInfo.InvariantCulture));
        if (Kind == SyncOperationKind.AdjustStock)
        {
            WriteField(stream, "delta", Delta, value => value.ToString(CultureInfo.InvariantCulture));
            WriteField(stream, "reason", Reason, value => value);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    public AdjustmentReceiptMetadata CreateAdjustmentMetadata(string? rejectionCode = null) => new(
        PresenceToken(Delta.State),
        Delta.State == WireFieldState.Value ? Delta.Value : null,
        PresenceToken(Reason.State),
        Reason.State == WireFieldState.Value ? Reason.Value : null,
        rejectionCode == "invalid_causal_fields"
            ? null
            : BaseServerRevision.State == WireFieldState.Value ? BaseServerRevision.Value : null,
        AppliedAgainstRevision: null,
        Rebased: null);

    public bool TryCreateCoreOperation(out ISyncProductOperation? operation, out string? rejectionCode)
    {
        operation = null;
        rejectionCode = null;

        var hasBase = BaseServerRevision.State == WireFieldState.Value;
        var hasPredecessor = PredecessorOperationId.State == WireFieldState.Value;
        if (Kind == SyncOperationKind.CreateProduct)
        {
            if (hasBase || hasPredecessor)
            {
                rejectionCode = "invalid_causal_fields";
                return false;
            }
        }
        else if (hasBase == hasPredecessor)
        {
            rejectionCode = "invalid_causal_fields";
            return false;
        }

        if (hasBase && BaseServerRevision.Value < 0)
        {
            rejectionCode = "invalid_base_revision";
            return false;
        }

        if (Kind == SyncOperationKind.AdjustStock)
        {
            if (Delta.State != WireFieldState.Value || Delta.Value == 0)
            {
                rejectionCode = "invalid_adjustment";
                return false;
            }

            if (Reason.State != WireFieldState.Value ||
                !SynchronizedAdjustmentReasons.IsKnown(Reason.Value))
            {
                rejectionCode = "invalid_reason";
                return false;
            }

            if (!SynchronizedAdjustmentReasons.IsSignAllowed(Reason.Value!, Delta.Value))
            {
                rejectionCode = "reason_sign_mismatch";
                return false;
            }
        }

        try
        {
            operation = Kind switch
            {
                SyncOperationKind.CreateProduct when
                    TryValue(Sku, out var sku) &&
                    TryValue(Description, out var createDescription) &&
                    TryValue(Price, out var createPrice) &&
                    TryValue(InitialStock, out var initialStock) =>
                    new CreateProductOperation(
                        OperationId,
                        ProductId,
                        sku!,
                        createDescription!,
                        createPrice,
                        initialStock,
                        CreatedAtUtc),
                SyncOperationKind.UpdateProductDetails when
                    TryValue(Description, out var updateDescription) &&
                    TryValue(Price, out var updatePrice) =>
                    new UpdateProductDetailsOperation(
                        OperationId,
                        ProductId,
                        updateDescription!,
                        updatePrice,
                        hasBase ? BaseServerRevision.Value : null,
                        hasPredecessor ? PredecessorOperationId.Value : null,
                        CreatedAtUtc),
                SyncOperationKind.ArchiveProduct =>
                    new ArchiveProductOperation(
                        OperationId,
                        ProductId,
                        hasBase ? BaseServerRevision.Value : null,
                        hasPredecessor ? PredecessorOperationId.Value : null,
                        CreatedAtUtc),
                SyncOperationKind.UnarchiveProduct =>
                    new UnarchiveProductOperation(
                        OperationId,
                        ProductId,
                        hasBase ? BaseServerRevision.Value : null,
                        hasPredecessor ? PredecessorOperationId.Value : null,
                        CreatedAtUtc),
                SyncOperationKind.AdjustStock =>
                    new AdjustStockOperation(
                        OperationId,
                        ProductId,
                        Delta.Value,
                        Reason.Value!,
                        hasBase ? BaseServerRevision.Value : null,
                        hasPredecessor ? PredecessorOperationId.Value : null,
                        CreatedAtUtc),
                _ => null
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or BusinessRuleException or OverflowException)
        {
            rejectionCode = "invalid_details";
            return false;
        }

        if (operation is null)
        {
            rejectionCode = "invalid_details";
            return false;
        }

        return true;
    }

    private static bool TryValue<T>(WireField<T> field, out T? value)
    {
        value = field.Value;
        return field.State == WireFieldState.Value;
    }

    private static string FormatGuid(Guid value) => value.ToString("D").ToLowerInvariant();

    private static string FormatLong(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string PresenceToken(WireFieldState state) => state switch
    {
        WireFieldState.Missing => "missing",
        WireFieldState.Null => "null",
        WireFieldState.Value => "value",
        _ => throw new InvalidOperationException("Unknown wire-field state.")
    };

    private static void WriteField<T>(
        Stream stream,
        string name,
        WireField<T> field,
        Func<T, string> format)
    {
        var state = field.State switch
        {
            WireFieldState.Missing => 'M',
            WireFieldState.Null => 'N',
            WireFieldState.Value => 'V',
            _ => throw new InvalidOperationException("Unknown wire-field state.")
        };
        var valueBytes = field.State == WireFieldState.Value
            ? Encoding.UTF8.GetBytes(format(field.Value!))
            : [];
        var prefix = Encoding.ASCII.GetBytes(
            $"{name}={state}:{valueBytes.Length.ToString(CultureInfo.InvariantCulture)}:");
        stream.Write(prefix);
        stream.Write(valueBytes);
        stream.WriteByte(10);
    }
}

internal static class UploadWireParser
{
    public static UploadWireEnvelope Parse(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireObject(root, "The upload envelope must be a JSON object.");
            EnsureUniqueProperties(root);

            var protocolVersion = ReadRequiredInt32(root, "protocolVersion");
            var serverInstanceId = ReadRequiredGuid(root, "serverInstanceId");
            if (!root.TryGetProperty("operations", out var operationsElement) ||
                operationsElement.ValueKind != JsonValueKind.Array)
            {
                throw new UploadWireException("invalid_envelope", "The operations array is required.");
            }

            var operations = operationsElement.EnumerateArray().Select(ParseOperation).ToList();
            if (operations.Count == 0)
            {
                throw new UploadWireException("invalid_batch", "At least one operation is required.");
            }

            return new UploadWireEnvelope(protocolVersion, serverInstanceId, operations);
        }
        catch (JsonException exception)
        {
            throw new UploadWireException("invalid_json", "The upload body is not valid JSON.", exception);
        }
    }

    private static UploadWireOperationDraft ParseOperation(JsonElement element)
    {
        RequireObject(element, "Every upload operation must be a JSON object.");
        EnsureUniqueProperties(element);

        var operationId = ReadRequiredGuid(element, "operationId");
        var productId = ReadRequiredGuid(element, "productId");
        var kindToken = ReadRequiredString(element, "kind");
        var kind = kindToken switch
        {
            "createProduct" => SyncOperationKind.CreateProduct,
            "updateProductDetails" => SyncOperationKind.UpdateProductDetails,
            "archiveProduct" => SyncOperationKind.ArchiveProduct,
            "unarchiveProduct" => SyncOperationKind.UnarchiveProduct,
            "adjustStock" => SyncOperationKind.AdjustStock,
            _ => throw new UploadWireException(
                "unsupported_operation",
                "The operation kind is not supported.")
        };
        var createdAtUtc = ReadRequiredUtcTimestamp(element, "createdAtUtc");
        var baseRevision = ReadOptionalInt64(element, "baseServerRevision");
        var predecessor = ReadOptionalGuid(element, "predecessorOperationId");

        WireField<string> sku = WireField<string>.Missing();
        WireField<string> description = WireField<string>.Missing();
        WireField<decimal> price = WireField<decimal>.Missing();
        WireField<int> initialStock = WireField<int>.Missing();
        WireField<int> delta = WireField<int>.Missing();
        WireField<string> reason = WireField<string>.Missing();
        if (element.TryGetProperty("details", out var details))
        {
            if (details.ValueKind != JsonValueKind.Object)
            {
                throw new UploadWireException("invalid_operation", "Operation details must be an object.");
            }

            EnsureUniqueProperties(details);
            sku = ReadOptionalString(details, "sku");
            description = ReadOptionalString(details, "description");
            price = ReadOptionalDecimal(details, "price");
            initialStock = ReadOptionalInt32(details, "initialStock");
            delta = ReadOptionalInt32(details, "delta");
            reason = ReadOptionalString(details, "reason");
        }

        return new UploadWireOperationDraft(
            operationId,
            kind,
            kindToken,
            productId,
            createdAtUtc,
            baseRevision,
            predecessor,
            sku,
            description,
            price,
            initialStock,
            delta,
            reason);
    }

    private static WireField<string> ReadOptionalString(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value)) return WireField<string>.Missing();
        if (value.ValueKind == JsonValueKind.Null) return WireField<string>.Null();
        if (value.ValueKind != JsonValueKind.String) throw Untypable(name);
        return WireField<string>.Present(value.GetString()!);
    }

    private static WireField<decimal> ReadOptionalDecimal(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value)) return WireField<decimal>.Missing();
        if (value.ValueKind == JsonValueKind.Null) return WireField<decimal>.Null();
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var result)) throw Untypable(name);
        return WireField<decimal>.Present(result);
    }

    private static WireField<int> ReadOptionalInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value)) return WireField<int>.Missing();
        if (value.ValueKind == JsonValueKind.Null) return WireField<int>.Null();
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result)) throw Untypable(name);
        return WireField<int>.Present(result);
    }

    private static WireField<long> ReadOptionalInt64(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value)) return WireField<long>.Missing();
        if (value.ValueKind == JsonValueKind.Null) return WireField<long>.Null();
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)) throw Untypable(name);
        return WireField<long>.Present(result);
    }

    private static WireField<Guid> ReadOptionalGuid(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value)) return WireField<Guid>.Missing();
        if (value.ValueKind == JsonValueKind.Null) return WireField<Guid>.Null();
        if (value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out var result)) throw Untypable(name);
        return WireField<Guid>.Present(result);
    }

    private static Guid ReadRequiredGuid(JsonElement owner, string name)
    {
        var text = ReadRequiredString(owner, name);
        if (!Guid.TryParse(text, out var value) || value == Guid.Empty) throw Untypable(name);
        return value;
    }

    private static string ReadRequiredString(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw Untypable(name);
        }

        return value.GetString()!;
    }

    private static int ReadRequiredInt32(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw Untypable(name);
        }

        return result;
    }

    private static DateTimeOffset ReadRequiredUtcTimestamp(JsonElement owner, string name)
    {
        var text = ReadRequiredString(owner, name);
        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var result) ||
            result.Offset != TimeSpan.Zero)
        {
            throw Untypable(name);
        }

        return result;
    }

    private static void RequireObject(JsonElement element, string message)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new UploadWireException("invalid_json", message);
        }
    }

    private static void EnsureUniqueProperties(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new UploadWireException(
                    "invalid_json",
                    $"The property '{property.Name}' appears more than once.");
            }
        }
    }

    private static UploadWireException Untypable(string fieldName) =>
        new("invalid_operation", $"The field '{fieldName}' is missing or has an invalid type.");
}

internal sealed class UploadWireException(
    string code,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
}
