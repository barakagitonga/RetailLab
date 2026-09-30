using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

[Authorize]
public class BasketModel(
    BasketService basket,
    CheckoutService checkout,
    CatalogDisplayMapper mapper,
    UserManager<IdentityUser> users) : PageModel
{
    private const string BasketPath = "/Basket";

    private const string CataloguePath = "/Products";

    public IReadOnlyList<BasketLine> Items { get; private set; } = [];

    public string BasketTotalDisplay { get; private set; } =
        CatalogDisplayMapper.FormatPrice(0);

    /// <summary>
    /// Fresh server-generated checkout-attempt identifier per render. It
    /// doubles as the new order's id, so a repeated submission resolves to
    /// the original order instead of creating another one.
    /// </summary>
    public Guid CheckoutAttemptId { get; private set; }

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

        var items = await basket.GetBasketItemsAsync(customerId);
        Items = items.Select(mapper.ToBasketLine).ToList();
        BasketTotalDisplay = CatalogDisplayMapper.FormatPrice(
            items.Sum(item => item.Product.Price * item.Quantity));
        CheckoutAttemptId = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string? sku, int? quantity, string? returnUrl)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        try
        {
            // Catalogue cards post quantity 1; the product page posts the
            // customer's chosen quantity. A missing value means "one".
            var item = await basket.AddAsync(customerId, sku ?? string.Empty, quantity ?? 1);
            StatusMessage = $"Added {item.Product.Description} to your basket.";
        }
        catch (BusinessRuleException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (BasketConflictException)
        {
            ErrorMessage = "Your basket changed; please try again.";
        }

        return RedirectToLocal(returnUrl, AddFallback(sku));
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? sku, int? quantity, string? returnUrl)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        try
        {
            // A missing or unparseable quantity binds as null; the service
            // rejects it with the same friendly message as zero.
            var item = await basket.UpdateAsync(customerId, sku ?? string.Empty, quantity ?? 0);
            StatusMessage = $"Updated {item.Product.Description} quantity to {item.Quantity}.";
        }
        catch (BusinessRuleException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (BasketConflictException)
        {
            ErrorMessage = "Your basket changed; please try again.";
        }

        return RedirectToLocal(returnUrl, BasketPath);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string? sku, string? returnUrl)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        try
        {
            var item = await basket.RemoveAsync(customerId, sku ?? string.Empty);
            StatusMessage = $"Removed {item.Product.Description} from your basket.";
        }
        catch (BusinessRuleException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (BasketConflictException)
        {
            ErrorMessage = "Your basket changed; please try again.";
        }

        return RedirectToLocal(returnUrl, BasketPath);
    }

    public async Task<IActionResult> OnPostCheckoutAsync(Guid? checkoutAttemptId)
    {
        var customerId = CurrentCustomerId();
        if (customerId is null)
        {
            return Challenge();
        }

        try
        {
            // A missing or unparseable attempt id binds as null; the service
            // rejects it as an expired checkout.
            var order = await checkout.CheckoutBasketAsync(customerId, checkoutAttemptId ?? Guid.Empty);
            StatusMessage = "Your simulated order was placed.";
            return RedirectToPage("/Orders/Details", new { id = order.Id });
        }
        catch (BusinessRuleException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (CheckoutConflictException)
        {
            ErrorMessage = "Your basket or a product changed; please review your basket and try again.";
        }

        return RedirectToLocal(returnUrl: null, BasketPath);
    }

    private string? CurrentCustomerId() => users.GetUserId(User);

    private IActionResult RedirectToLocal(string? returnUrl, string fallback) =>
        LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : fallback);

    private static string AddFallback(string? sku) =>
        string.IsNullOrWhiteSpace(sku)
            ? CataloguePath
            : $"/Products/{sku.Trim()}";
}
