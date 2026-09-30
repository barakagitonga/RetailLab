namespace RetailLab.Web.Models;

/// <summary>
/// One simulated order for the confirmation page, with its snapshot lines.
/// Carries pre-formatted display strings so Razor markup never touches
/// entities or decimals.
/// </summary>
public sealed class OrderDetails
{
    public required Guid Id { get; init; }

    public required string PlacedAtDisplay { get; init; }

    public required string TotalDisplay { get; init; }

    public required IReadOnlyList<OrderLineItem> Lines { get; init; }
}
