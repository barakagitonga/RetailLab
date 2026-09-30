using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages.Orders;

[Authorize]
public class IndexModel(
    CheckoutService checkout,
    CatalogDisplayMapper mapper,
    UserManager<IdentityUser> users) : PageModel
{
    public IReadOnlyList<OrderSummary> Orders { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var customerId = users.GetUserId(User);
        if (customerId is null)
        {
            return Challenge();
        }

        var orders = await checkout.GetOrdersAsync(customerId);
        Orders = orders.Select(mapper.ToOrderSummary).ToList();
        return Page();
    }
}
