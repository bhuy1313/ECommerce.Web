using System.Net;
using System.Text;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Web.Tests.Services;

/// <summary>
/// Test CurrencyService display-only: toán quy đổi, fallback VNĐ, cookie.
/// Frankfurter được mock qua HttpMessageHandler, không gọi mạng thật.
/// </summary>
public class CurrencyServiceTests : IDisposable
{
    private const string RatesJson = """
        [
          {"date":"2026-10-11","base":"VND","quote":"USD","rate":0.00004},
          {"date":"2026-10-11","base":"VND","quote":"EUR","rate":0.000035},
          {"date":"2026-10-11","base":"VND","quote":"JPY","rate":0.006}
        ]
        """;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private bool _disposed;

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private CurrencyService CreateService(HttpMessageHandler handler, string? cookieCode = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
        var context = new DefaultHttpContext();
        if (cookieCode != null)
        {
            // Tên cookie phải khớp CurrencyService.CookieName ("technova-currency").
            context.Request.Headers.Cookie = $"technova-currency={cookieCode}";
        }
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new CurrencyService(http, _cache, accessor, NullLogger<CurrencyService>.Instance, EmptyConfig());
    }

    private static HttpMessageHandler OkHandler(string json) =>
        new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    private static HttpMessageHandler FailHandler() =>
        new StubHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));

    [Fact]
    public async Task FormatAsync_DefaultVnd_KeepsLegacyFormat()
    {
        var svc = CreateService(OkHandler(RatesJson));

        var result = await svc.FormatAsync(24990000m);

        Assert.Equal("24,990,000 đ", result);
    }

    [Fact]
    public async Task FormatAsync_UsdCookie_ConvertsWithTwoDecimals()
    {
        var svc = CreateService(OkHandler(RatesJson), "USD");

        // 25.000.000 VNĐ × 0.00004 = $1,000.00
        var result = await svc.FormatAsync(25000000m);

        Assert.Equal("$1,000.00", result);
    }

    [Fact]
    public async Task FormatAsync_JpyCookie_UsesZeroDecimals()
    {
        var svc = CreateService(OkHandler(RatesJson), "JPY");

        // 1.000.000 VNĐ × 0.006 = ¥6,000
        var result = await svc.FormatAsync(1000000m);

        Assert.Equal("¥6,000", result);
    }

    [Fact]
    public async Task FormatAsync_UnknownCookieCode_FallsBackToVnd()
    {
        var svc = CreateService(OkHandler(RatesJson), "XXX");

        var result = await svc.FormatAsync(1000000m);

        Assert.Equal("1,000,000 đ", result);
    }

    [Fact]
    public async Task FormatAsync_ApiFailure_FallsBackToVndWithoutThrowing()
    {
        var svc = CreateService(FailHandler(), "USD");

        var result = await svc.FormatAsync(1000000m);

        Assert.Equal("1,000,000 đ", result);
    }

    [Fact]
    public void Supported_ContainsExpectedEightCurrencies()
    {
        var svc = CreateService(OkHandler(RatesJson));

        var codes = svc.Supported.Select(c => c.Code).ToList();

        Assert.Equal(new[] { "VND", "USD", "EUR", "GBP", "JPY", "KRW", "CNY", "SGD" }, codes);
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("VND", true)]
    [InlineData("XXX", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsSupported_ValidatesCodes(string? code, bool expected)
    {
        var svc = CreateService(OkHandler(RatesJson));

        Assert.Equal(expected, svc.IsSupported(code));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Dispose();
            _disposed = true;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }
}
