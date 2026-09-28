using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

[Authorize]
public class FavouritesModel(
    BookmarkService bookmarks,
    CatalogService catalog,
    CatalogDisplayMapper mapper,
    UserManager<IdentityUser> users) : PageModel
{
    public IReadOnlyList<ProductSummary> Items { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        var saved = await bookmarks.GetBookmarksAsync(customerId);
        Items = saved.Select(bookmark => mapper.ToSummary(bookmark.Product)).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string? sku, string? returnUrl)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        var product = await catalog.FindActiveProductAsync(sku ?? string.Empty);
        if (product is null)
        {
            ErrorMessage = "That product is no longer available.";
            return RedirectToLocal(returnUrl, FavouriteRedirects.CataloguePath);
        }

        var fallback = FavouriteRedirects.AddFallback(product.Sku);
        var saved = await bookmarks.GetBookmarksAsync(customerId);
        if (Contains(saved, product.Sku))
        {
            StatusMessage = $"{product.Description} is already in your favourites.";
            return RedirectToLocal(returnUrl, fallback);
        }

        try
        {
            await bookmarks.AddAsync(customerId, product.Sku);
            StatusMessage = $"Saved {product.Description} to your favourites.";
        }
        catch (BusinessRuleException)
        {
            // Lost race (for example, archived between lookup and insert).
            ErrorMessage = $"Could not save {product.Description}. Please try again.";
        }

        return RedirectToLocal(returnUrl, fallback);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string? sku, string? returnUrl)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        var product = await catalog.FindActiveProductAsync(sku ?? string.Empty);
        if (product is null)
        {
            ErrorMessage = "That product is no longer available.";
            return RedirectToLocal(returnUrl, FavouriteRedirects.FavouritesPath);
        }

        var saved = await bookmarks.GetBookmarksAsync(customerId);
        if (!Contains(saved, product.Sku))
        {
            StatusMessage = $"{product.Description} is not in your favourites.";
            return RedirectToLocal(returnUrl, FavouriteRedirects.FavouritesPath);
        }

        try
        {
            await bookmarks.RemoveAsync(customerId, product.Sku);
            StatusMessage = $"Removed {product.Description} from your favourites.";
        }
        catch (BusinessRuleException)
        {
            // Lost race (for example, removed in another tab).
            ErrorMessage = $"Could not remove {product.Description}. Please try again.";
        }

        return RedirectToLocal(returnUrl, FavouriteRedirects.FavouritesPath);
    }

    private string? CurrentCustomerId() => users.GetUserId(User);

    private IActionResult RedirectToLocal(string? returnUrl, string fallback) =>
        LocalRedirect(FavouriteRedirects.Resolve(returnUrl, Url.IsLocalUrl, fallback));

    private static bool Contains(IReadOnlyList<Bookmark> saved, string sku) =>
        saved.Any(bookmark =>
            string.Equals(bookmark.Product.Sku, sku, StringComparison.OrdinalIgnoreCase));
}
