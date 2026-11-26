using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class IndexModel(
    CatalogService catalog,
    CatalogDisplayMapper mapper,
    ProductImageResolver images) : PageModel
{
    public IReadOnlyList<ProductCard> Featured { get; private set; } = [];

    public IReadOnlyList<ProductCard> NewArrivals { get; private set; } = [];

    public IReadOnlyList<ProductCard> BestSellers { get; private set; } = [];

    public IReadOnlyList<LandingReviewCard> LandingReviews { get; private set; } = [];

    /// <summary>
    /// All active products for the slide-A scrolling collage strip.
    /// Read-only, populated in OnGetAsync alongside the other lists.
    /// </summary>
    public IReadOnlyList<ProductCard> AllProducts { get; private set; } = [];

    /// <summary>
    /// One demo-review line per product for the landing reviews panel.
    /// Reuses the existing DemoProductContent store (S3 pattern); it adds
    /// no database, endpoint, or new demo data source. Author/rating/time
    /// fields are deliberately NOT invented here - they arrive with the
    /// dedicated demo-review data checkpoint and stay absent until then.
    /// </summary>
    public sealed record LandingReviewCard(string Sku, string ProductName, string Text);

    public async Task OnGetAsync()
    {
        var active = await catalog.GetActiveProductsAsync();

        // real products only; a section shows a SKU only while the customer
        // read path can still prove it (archived/removed products drop out).
        var summaries = new Dictionary<string, ProductSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in active)
        {
            summaries.TryAdd(product.Sku, mapper.ToSummary(product));
        }

        Featured = Cards(summaries, StorefrontMerchandising.FeaturedProductSkus);
        NewArrivals = Cards(summaries, StorefrontMerchandising.NewArrivalSkus);
        BestSellers = Cards(summaries, StorefrontMerchandising.BestSellerSkus);
        LandingReviews = BuildLandingReviews(summaries);
        AllProducts = Cards(summaries, summaries.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    private static List<LandingReviewCard> BuildLandingReviews(
        IReadOnlyDictionary<string, ProductSummary> summaries)
    {
        return summaries.Values
            .OrderBy(s => s.Sku, StringComparer.Ordinal)
            .Select(s => new LandingReviewCard(
                s.Sku,
                s.Description,
                DemoProductContent.ReviewsBySku.TryGetValue(s.Sku, out var lines) && lines.Length > 0
                    ? lines[0]
                    : null!))
            .Where(review => review.Text is not null)
            .ToList();
    }

    private List<ProductCard> Cards(
        IReadOnlyDictionary<string, ProductSummary> summaries,
        IEnumerable<string> arrangement)
    {
        return arrangement
            .Where(summaries.ContainsKey)
            .Select(sku => ProductCard.From(summaries[sku], images.Resolve(sku)))
            .ToList();
    }
}
