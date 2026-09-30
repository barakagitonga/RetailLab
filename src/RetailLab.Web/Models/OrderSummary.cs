namespace RetailLab.Web.Models;

/// <summary>
/// One order row on the order-history page. Carries pre-formatted display
/// strings so Razor markup never touches entities or decimals.
/// </summary>
public sealed class OrderSummary
{
    public required Guid Id { get; init; }

    public required string PlacedAtDisplay { get; init; }

    public required string ItemsDisplay { get; init; }

    public required string TotalDisplay { get; init; }
}
