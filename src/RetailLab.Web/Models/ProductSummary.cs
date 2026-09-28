namespace RetailLab.Web.Models;

/// <summary>
/// Customer-facing product card. Carries pre-formatted display strings so Razor
/// markup never touches entities, decimals, or stock counts.
/// </summary>
public sealed class ProductSummary
{
    public required string Sku { get; init; }

    public required string Description { get; init; }

    public required string PriceDisplay { get; init; }

    public required string Availability { get; init; }

    /// <summary>CSS tone for the badge: "low" for low/out-of-stock, otherwise empty.</summary>
    public required string AvailabilityTone { get; init; }

    public bool IsOutOfStock { get; init; }
}
