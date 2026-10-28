using ECommerce.Web.Models.Configuration;
using Xunit;

namespace ECommerce.Web.Tests.Models;

public class VnPayOptionsTests
{
    [Fact]
    public void IsConfigured_WhenAllRequiredFieldsPresent_ReturnsTrue()
    {
        var options = new VnPayOptions
        {
            TmnCode = "TMN_CODE",
            HashSecret = "SECRET_HASH",
            ReturnUrl = "https://example.com/payment/vnpay-return",
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
        };

        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData("", "SECRET", "https://example.com")]
    [InlineData("TMN", "", "https://example.com")]
    [InlineData("TMN", "SECRET", "")]
    [InlineData(null, "SECRET", "https://example.com")]
    [InlineData("TMN", null, "https://example.com")]
    [InlineData("TMN", "SECRET", null)]
    public void IsConfigured_WhenAnyRequiredFieldMissing_ReturnsFalse(string? tmn, string? secret, string? returnUrl)
    {
        var options = new VnPayOptions
        {
            TmnCode = tmn!,
            HashSecret = secret!,
            ReturnUrl = returnUrl!
        };

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("   ", "SECRET", "https://example.com")]
    [InlineData("TMN", "   ", "https://example.com")]
    [InlineData("TMN", "SECRET", "   ")]
    public void IsConfigured_WhenFieldOnlyWhitespace_ReturnsFalse(string tmn, string secret, string returnUrl)
    {
        var options = new VnPayOptions
        {
            TmnCode = tmn,
            HashSecret = secret,
            ReturnUrl = returnUrl
        };

        Assert.False(options.IsConfigured);
    }
}
