namespace RetailLab.Web.Models;

/// <summary>
/// One snapshot line of a placed simulated order. Carries pre-formatted
/// display strings so Razor markup never touches entities or decimals.
/// </summary>
public sealed class OrderLineItem
{
    public required string Sku { get; init; }

    public required string Description { get; init; }

    public required int Quantity { get; init; }

    public required string UnitPriceDisplay { get; init; }

    public required string LineTotalDisplay { get; init; }
}
