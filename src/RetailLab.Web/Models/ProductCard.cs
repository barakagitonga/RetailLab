using System.Globalization;
using RetailLab.Web.Services;

namespace RetailLab.Web.Models;

/// <summary>
/// Customer-facing catalogue card: <see cref="ProductSummary"/> facts plus the
/// convention-resolved image. <see cref="HasImage"/> is false when the SKU has
/// no local image file, so markup renders the placeholder tile instead of a
/// broken link.
/// </summary>
public sealed class ProductCard
{
    public required string Sku { get; init; }

    public required string Description { get; init; }

    public required string PriceDisplay { get; init; }

    public required string Availability { get; init; }

    public required string AvailabilityTone { get; init; }

    public bool IsOutOfStock { get; init; }

    public string? ImageUrl { get; init; }

    public bool HasImage => ImageUrl is not null;

    public string? DemoRatingDisplay { get; init; }

    public int DemoReviewCount { get; init; }

    public bool HasDemoReviews => DemoReviewCount > 0;

    public string DemoReviewCountDisplay =>
        DemoReviewCount == 1 ? "1 review" : $"{DemoReviewCount} reviews";

    public bool IsHotDemoProduct { get; init; }

    public static ProductCard From(ProductSummary summary, string? imageUrl)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var reviews = DemoProductContent.GetReviewSummary(summary.Sku);

        return new ProductCard
        {
            Sku = summary.Sku,
            Description = summary.Description,
            PriceDisplay = summary.PriceDisplay,
            Availability = summary.Availability,
            AvailabilityTone = summary.AvailabilityTone,
            IsOutOfStock = summary.IsOutOfStock,
            ImageUrl = imageUrl,
            DemoRatingDisplay = reviews.Count == 0
                ? null
                : reviews.AverageRating.ToString("0.0", CultureInfo.InvariantCulture),
            DemoReviewCount = reviews.Count,
            IsHotDemoProduct = DemoProductContent.IsHotDemoProduct(summary.Sku)
        };
    }
}
