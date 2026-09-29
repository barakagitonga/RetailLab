using System.Globalization;
using RetailLab.Core;
using RetailLab.Web.Models;

namespace RetailLab.Web.Services;

/// <summary>
/// Translates Core products into web display models. Owns this tutorial's
/// presentation conventions (USD formatting, availability bands) so Core stays
/// locale-free and Razor markup stays logic-free.
/// </summary>
public sealed class CatalogDisplayMapper
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Stock level at or below which a product reads as "Low stock".
    /// </summary>
    public const int LowStockThreshold = 5;

    public static string FormatPrice(decimal price) => price.ToString("C", UsCulture);

    public static string AvailabilityFor(int stockQuantity) =>
        stockQuantity <= 0 ? "Out of stock"
        : stockQuantity <= LowStockThreshold ? "Low stock"
        : "In stock";

    public static string AvailabilityToneFor(int stockQuantity) =>
        stockQuantity <= LowStockThreshold ? "low" : string.Empty;

    public ProductSummary ToSummary(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductSummary
        {
            Sku = product.Sku,
            Description = product.Description,
            PriceDisplay = FormatPrice(product.Price),
            Availability = AvailabilityFor(product.StockQuantity),
            AvailabilityTone = AvailabilityToneFor(product.StockQuantity),
            IsOutOfStock = product.StockQuantity <= 0
        };
    }

    public ProductDetails ToDetails(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductDetails
        {
            Sku = product.Sku,
            Description = product.Description,
            PriceDisplay = FormatPrice(product.Price),
            Availability = AvailabilityFor(product.StockQuantity),
            AvailabilityTone = AvailabilityToneFor(product.StockQuantity),
            IsOutOfStock = product.StockQuantity <= 0
        };
    }

    public BasketLine ToBasketLine(BasketItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Product);

        var product = item.Product;
        return new BasketLine
        {
            Sku = product.Sku,
            Description = product.Description,
            PriceDisplay = FormatPrice(product.Price),
            Availability = AvailabilityFor(product.StockQuantity),
            AvailabilityTone = AvailabilityToneFor(product.StockQuantity),
            Quantity = item.Quantity,
            LineTotalDisplay = FormatPrice(product.Price * item.Quantity),
            IsArchived = product.IsArchived
        };
    }
}
