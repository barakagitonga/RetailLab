using RetailLab.Web.Services;

namespace RetailLab.Tests;

public sealed class FavouriteRedirectTests
{
    private static bool LocalOnly(string? url) => url == "/Products/AUR-100";

    [Fact]
    public void AddFallback_ReturnsProductPageForKnownSku()
    {
        Assert.Equal("/Products/AUR-100", FavouriteRedirects.AddFallback("AUR-100"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddFallback_ReturnsCatalogueWhenSkuUnknown(string? sku)
    {
        Assert.Equal(FavouriteRedirects.CataloguePath, FavouriteRedirects.AddFallback(sku));
    }

    [Fact]
    public void Resolve_HonorsLocalReturnUrl()
    {
        Assert.Equal(
            "/Products/AUR-100",
            FavouriteRedirects.Resolve(
                "/Products/AUR-100",
                LocalOnly,
                FavouriteRedirects.FavouritesPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example/steal")]
    [InlineData("/Favourites")]
    public void Resolve_FallsBackWhenReturnUrlMissingOrRejected(string? returnUrl)
    {
        // "/Favourites" is rejected here only because the stub predicate
        // accepts nothing but the product page; at runtime the framework's
        // Url.IsLocalUrl is the predicate, and it accepts local paths.
        Assert.Equal(
            FavouriteRedirects.CataloguePath,
            FavouriteRedirects.Resolve(returnUrl, LocalOnly, FavouriteRedirects.CataloguePath));
    }

    [Fact]
    public void Resolve_RequiresLocalityCheck()
    {
        Assert.Throws<ArgumentNullException>(
            () => FavouriteRedirects.Resolve("/Products", null!, FavouriteRedirects.CataloguePath));
    }
}
