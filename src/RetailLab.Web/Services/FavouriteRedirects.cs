namespace RetailLab.Web.Services;

/// <summary>
/// Return-URL policy for the favourites flow. PageModels pass the framework's
/// <c>Url.IsLocalUrl</c> as the locality check so open redirects are rejected;
/// the fallback keeps the customer on a sensible page. Kept as pure functions
/// so the branching is unit-testable without MVC plumbing.
/// </summary>
public static class FavouriteRedirects
{
    public const string CataloguePath = "/Products";

    public const string FavouritesPath = "/Favourites";

    /// <summary>
    /// Where an Add POST lands when its return URL is missing or unsafe: the
    /// matching product-details page, or the catalogue when no SKU is known.
    /// </summary>
    public static string AddFallback(string? sku) =>
        string.IsNullOrWhiteSpace(sku)
            ? CataloguePath
            : $"/Products/{sku.Trim()}";

    /// <summary>
    /// Honors <paramref name="returnUrl" /> only when
    /// <paramref name="isLocalUrl" /> accepts it; otherwise uses
    /// <paramref name="fallback" />.
    /// </summary>
    public static string Resolve(
        string? returnUrl,
        Func<string?, bool> isLocalUrl,
        string fallback)
    {
        ArgumentNullException.ThrowIfNull(isLocalUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        return !string.IsNullOrEmpty(returnUrl) && isLocalUrl(returnUrl)
            ? returnUrl
            : fallback;
    }
}
