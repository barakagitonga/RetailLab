namespace RetailLab.Web.Models;

/// <summary>
/// One customer-facing basket row. Carries pre-formatted display strings so
/// Razor markup never touches entities, decimals, or stock counts.
/// </summary>
public sealed class BasketLine
{
    public required string Sku { get; init; }

    public required string Description { get; init; }

    public required string PriceDisplay { get; init; }

    public required string Availability { get; init; }

    /// <summary>CSS tone for the badge: "low" for low/out-of-stock, otherwise empty.</summary>
    public required string AvailabilityTone { get; init; }

    public required int Quantity { get; init; }

    public required string LineTotalDisplay { get; init; }

    /// <summary>
    /// Archived products stay in the basket with a "no longer available" note
    /// and removal as the only action.
    /// </summary>
    public required bool IsArchived { get; init; }
}
