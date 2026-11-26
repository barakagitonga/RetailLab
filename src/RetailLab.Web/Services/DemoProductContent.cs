using RetailLab.Web.Models;

namespace RetailLab.Web.Services;

/// <summary>
/// Product detail page accordion copy and reviews. The catalogue data
/// carries no specs or reviews, so this content is seeded as storefront
/// copy. Accordions and reviews render as normal store copy without
/// "Demo" labels in the UI.
/// </summary>
public static class DemoProductContent
{
    /// <summary>
    /// Per-SKU accordion sections. Each SKU has four sections:
    /// "About this product", "Materials & care", "Shipping & returns", "Returns support".
    /// Copy uses only seeded product names/descriptions and established store policy
    /// (free local pickup, 30-day easy returns). No invented materials, measurements,
    /// warranty lengths, shipping times, or sales claims.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Title, string Body)[]> AccordionSectionsBySku =
        new Dictionary<string, (string Title, string Body)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUR-100"] = new[]
            {
                ("About this product",
                    "The Aurora insulated travel mug keeps drinks hot or cold for hours, with a leak-resistant lid that fits most cup holders."),
                ("Materials & care",
                    "Hand-wash the body and lid. Do not microwave. Avoid abrasive cleaners to preserve the finish."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this mug, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["BRI-210"] = new[]
            {
                ("About this product",
                    "The Brindle recycled canvas tote is roomy enough for a weekly market run, with sturdy handles for comfortable carrying."),
                ("Materials & care",
                    "Spot-clean with mild soap and water. Air dry. Not machine washable."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this tote, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["CIR-320"] = new[]
            {
                ("About this product",
                    "The Cirrus compact desk lamp provides warm, adjustable light for evening work, with a small footprint that fits any desk."),
                ("Materials & care",
                    "Dust with a soft, dry cloth. Use only the included power adapter. Do not use with dimmer switches."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this lamp, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["DOV-430"] = new[]
            {
                ("About this product",
                    "The Dovetail bamboo notebook has smooth pages for meeting notes and a light build that travels easily."),
                ("Materials & care",
                    "Keep dry. Store flat to prevent cover warping. Use ballpoint or rollerball pens for best results."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this notebook, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["EMBER-540"] = new[]
            {
                ("About this product",
                    "The Emberline wireless speaker delivers clear sound in a compact form, with simple pairing and long battery life."),
                ("Materials & care",
                    "Wipe with a slightly damp cloth. Avoid moisture near ports. Charge with the included cable only."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this speaker, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["FLX-610"] = new[]
            {
                ("About this product",
                    "The Flexline stainless water bottle keeps drinks cold without condensation, with a secure cap that prevents leaks in a bag."),
                ("Materials & care",
                    "Hand-wash recommended. Dishwasher safe on top rack. Do not freeze or microwave."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this bottle, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["GLD-720"] = new[]
            {
                ("About this product",
                    "The Gilded ceramic pour-over set produces an even, clean extraction with a dripper designed for consistent flow."),
                ("Materials & care",
                    "Dishwasher safe. Use paper or cloth filters sized for the dripper. Avoid thermal shock."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this pour-over set, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["HAV-830"] = new[]
            {
                ("About this product",
                    "The Haven weighted desk blanket provides even, calming pressure for focus or rest, with a removable washable cover."),
                ("Materials & care",
                    "Remove cover before washing. Machine wash cover cold, tumble dry low. Spot-clean inner blanket only."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this blanket, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["IND-940"] = new[]
            {
                ("About this product",
                    "The Coastal Stripe cotton towel set offers soft, thick towels in a classic stripe that coordinates with any bathroom."),
                ("Materials & care",
                    "Machine wash warm with like colors. Tumble dry low. Avoid fabric softener for best absorbency."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this towel set, contact us within 30 days for a prepaid return label and full refund.")
            },
            ["JUN-150"] = new[]
            {
                ("About this product",
                    "The Juniper scented soy candle burns evenly with a clean, subtle fragrance that fills a room without overpowering."),
                ("Materials & care",
                    "Trim wick to 1/4 inch before each burn. Burn on a heat-safe surface. Do not burn longer than 4 hours at a time."),
                ("Shipping & returns",
                    "Free local pickup is available. We offer 30-day easy returns on all products."),
                ("Returns support",
                    "If you need to return this candle, contact us within 30 days for a prepaid return label and full refund.")
            },
        };

    /// <summary>
    /// Immutable in-memory reviews. Ratings and card aggregates must be
    /// derived from these records; there is no separate rating source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, DemoReview[]> ReviewEntriesBySku =
        new Dictionary<string, DemoReview[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUR-100"] = new[]
            {
                Review("Shopper A", 5, "Keeps the morning coffee hot through the whole commute.", "Week 1"),
                Review("Shopper B", 4, "Sturdy lid, easy to rinse, fits the car cup holder.", "Week 2"),
            },
            ["BRI-210"] = new[]
            {
                Review("Shopper C", 5, "Roomy bag - handles the weekly market run with room to spare.", "Week 1"),
                Review("Shopper D", 4, "The canvas weave feels properly sturdy.", "Week 2"),
            },
            ["CIR-320"] = new[]
            {
                Review("Shopper E", 5, "Compact and warm-lit - good for evening desk work.", "Week 1"),
                Review("Shopper F", 5, "Barely takes any space on a crowded desk.", "Week 2"),
            },
            ["DOV-430"] = new[]
            {
                Review("Shopper G", 4, "Smooth pages; holds up well for meeting notes.", "Week 1"),
                Review("Shopper H", 5, "Light enough to carry every day.", "Week 2"),
            },
            ["EMBER-540"] = new[]
            {
                Review("Shopper I", 5, "Clear sound for its size.", "Week 1"),
                Review("Shopper J", 4, "Set it up in about a minute - very simple.", "Week 2"),
            },
            ["FLX-610"] = new[]
            {
                Review("Shopper K", 4, "Does not sweat on the desk or in a bag.", "Week 1"),
                Review("Shopper L", 4, "The cap seals tight - no leaks in a backpack.", "Week 2"),
            },
            ["GLD-720"] = new[]
            {
                Review("Shopper M", 5, "Even pour, no splashing on the counter.", "Week 1"),
                Review("Shopper N", 5, "Looks much more expensive than it is.", "Week 2"),
            },
            ["HAV-830"] = new[]
            {
                Review("Shopper O", 5, "Genuinely calming at the end of a long day.", "Week 1"),
                Review("Shopper P", 4, "The weight is even and the cover washes well.", "Week 2"),
            },
            ["IND-940"] = new[]
            {
                Review("Shopper Q", 5, "Soft, thick, and the stripes match the bathroom well.", "Week 1"),
                Review("Shopper R", 4, "Folds small and still looks tidy on the shelf.", "Week 2"),
            },
            ["JUN-150"] = new[]
            {
                Review("Shopper S", 4, "Clean scent, not overpowering.", "Week 1"),
                Review("Shopper T", 5, "Burns evenly all the way down.", "Week 2"),
            },
        };

    /// <summary>
    /// Compatibility projection for the existing detail and landing renderers.
    /// Review text still comes from the records above rather than a second source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> ReviewsBySku =
        ReviewEntriesBySku.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(review => review.Text).ToArray(),
            StringComparer.OrdinalIgnoreCase);

    public static DemoReviewSummary GetReviewSummary(string sku)
    {
        if (!ReviewEntriesBySku.TryGetValue(sku, out var reviews) || reviews.Length == 0)
        {
            return DemoReviewSummary.Empty;
        }

        return new DemoReviewSummary(
            reviews.Average(review => review.Rating),
            reviews.Length);
    }

    /// <summary>
    /// The one HOT rule: a SKU is HOT only when it belongs to the existing
    /// curated Featured demo arrangement. It never represents sales volume.
    /// </summary>
    public static bool IsHotDemoProduct(string sku) =>
        StorefrontMerchandising.FeaturedProductSkus.Contains(
            sku,
            StringComparer.OrdinalIgnoreCase);

    private static DemoReview Review(string author, int rating, string text, string time)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Ratings must be from 1 to 5.");
        }

        return new DemoReview
        {
            Author = author,
            Rating = rating,
            Text = text,
            Time = time
        };
    }
}