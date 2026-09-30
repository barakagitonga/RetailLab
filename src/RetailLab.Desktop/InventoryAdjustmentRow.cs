using System.Globalization;
using RetailLab.Core;

namespace RetailLab.Desktop;

/// <summary>
/// Staff-facing display data for one stock adjustment. Keeping this separate
/// from <see cref="InventoryAdjustment"/> means WPF formatting and labels do
/// not become business-domain concerns. No business rules live here.
/// </summary>
public sealed record InventoryAdjustmentRow(
    DateTimeOffset CreatedAtUtc,
    int QuantityChange,
    int ResultingQuantity,
    string Reason,
    string ActorIdentifier)
{
    /// <summary>
    /// UTC timestamp in a stable staff-readable form.
    /// </summary>
    public string CreatedAtUtcDisplay =>
        CreatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Signed quantity change such as +5 or -3.
    /// </summary>
    public string QuantityChangeDisplay =>
        QuantityChange.ToString("+0;-0", CultureInfo.InvariantCulture);

    public static InventoryAdjustmentRow FromAdjustment(InventoryAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);

        return new(
            adjustment.CreatedAtUtc,
            adjustment.QuantityChange,
            adjustment.ResultingQuantity,
            adjustment.Reason,
            adjustment.ActorIdentifier);
    }
}
