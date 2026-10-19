using ECommerce.Web.Services.Email;

namespace ECommerce.Web.Tests.Services.Email;

public class IdentityTokenCodecTests
{
    [Fact]
    public void EncodeThenTryDecode_RoundTripsUrlHostileToken()
    {
        const string raw = "CfDJ8+abc/def==";

        var code = IdentityTokenCodec.Encode(raw);

        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
        Assert.True(IdentityTokenCodec.TryDecode(code, out var decoded));
        Assert.Equal(raw, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%%%")]
    [InlineData("a*b")]
    public void TryDecode_RejectsMissingOrNonBase64UrlInput(string? code)
    {
        Assert.False(IdentityTokenCodec.TryDecode(code, out var token));
        Assert.Equal(string.Empty, token);
    }
}
