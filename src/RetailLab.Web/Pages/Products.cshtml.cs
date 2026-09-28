using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class ProductsModel(
    CatalogService catalog,
    CatalogDisplayMapper mapper,
    BookmarkService bookmarks,
    UserManager<IdentityUser> users) : PageModel
{
    public IReadOnlyList<ProductSummary> Products { get; private set; } = [];

    public IReadOnlySet<string> FavouriteSkus { get; private set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public async Task OnGetAsync()
    {
        var active = await catalog.GetActiveProductsAsync();
        Products = active.Select(mapper.ToSummary).ToList();

        // Anonymous visitors get no favourite state; the view shows them a
        // sign-in link instead of a form, so no POST is ever at stake.
        var customerId = users.GetUserId(User);
        if (customerId is not null)
        {
            var saved = await bookmarks.GetBookmarksAsync(customerId);
            FavouriteSkus = saved
                .Select(bookmark => bookmark.Product.Sku)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
