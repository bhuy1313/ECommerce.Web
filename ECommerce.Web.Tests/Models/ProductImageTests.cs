using ECommerce.Web.Models;

namespace ECommerce.Web.Tests.Models;

public class ProductImageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenImageUrlMissing_ReturnsPlaceholder(string? imageUrl)
    {
        Assert.Equal(ProductImage.Placeholder, ProductImage.Resolve(imageUrl));
    }

    [Theory]
    [InlineData("/img/x.png", "/img/x.png")]
    [InlineData("https://cdn.example.com/x.jpg", "https://cdn.example.com/x.jpg")]
    [InlineData("  /img/x.png  ", "/img/x.png")]
    public void Resolve_WhenImageUrlPresent_ReturnsTrimmedUrl(string imageUrl, string expected)
    {
        Assert.Equal(expected, ProductImage.Resolve(imageUrl));
    }
}
