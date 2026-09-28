namespace RetailLab.Web.Models;

/// <summary>
/// Customer-facing product details. Uses the single Core description honestly;
/// this slice invents no additional product copy.
/// </summary>
public sealed class ProductDetails
{
    public required string Sku { get; init; }

    public required string Description { get; init; }

    public required string PriceDisplay { get; init; }

    public required string Availability { get; init; }

    /// <summary>CSS tone for the badge: "low" for low/out-of-stock, otherwise empty.</summary>
    public required string AvailabilityTone { get; init; }

    public bool IsOutOfStock { get; init; }
}
