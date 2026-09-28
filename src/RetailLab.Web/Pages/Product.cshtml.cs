using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class ProductModel(
    CatalogService catalog,
    CatalogDisplayMapper mapper,
    BookmarkService bookmarks,
    UserManager<IdentityUser> users) : PageModel
{
    public ProductDetails? Product { get; private set; }

    public bool IsFavourite { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? sku)
    {
        var product = await catalog.FindActiveProductAsync(sku ?? string.Empty);
        if (product is null)
        {
            return NotFound();
        }

        Product = mapper.ToDetails(product);

        var customerId = users.GetUserId(User);
        if (customerId is not null)
        {
            var saved = await bookmarks.GetBookmarksAsync(customerId);
            IsFavourite = saved.Any(bookmark =>
                string.Equals(bookmark.Product.Sku, product.Sku, StringComparison.OrdinalIgnoreCase));
        }

        return Page();
    }
}
