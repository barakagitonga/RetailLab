namespace RetailLab.Web.Services;

/// <summary>
/// Demo merchandising arrangements for the storefront (storefront brief
/// section 10, checkpoint S2; Muse ruling on validation question Q2).
/// These lists are DECOR: the catalogue data carries no category, timestamp,
/// or sales-counter signal they could claim to reflect. Every section built
/// from them renders with a visible "Demo" label and phrasing that never
/// asserts a signal the data does not have ("demo arrangement", never
/// "newest" or "best-selling" as fact). Sections are filtered at render time
/// so a SKU the customer read path can no longer prove is never shown.
/// </summary>
public static class StorefrontMerchandising
{
    public static readonly string[] FeaturedProductSkus = { "CIR-320", "AUR-100", "EMBER-540" };

    public static readonly string[] NewArrivalSkus = { "DOV-430", "BRI-210", "CIR-320" };

    public static readonly string[] BestSellerSkus = { "AUR-100", "EMBER-540", "DOV-430" };
}
