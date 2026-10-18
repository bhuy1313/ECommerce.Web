using System.Net;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ECommerce.Web.Tests.Services;

public class VnPayGatewayTests
{
    private const string AmountParam = "10000000";
    private const string TxnRef = "5-1700000000000";

    private static VnPayOptions Options() => new()
    {
        TmnCode = "T",
        HashSecret = "secret",
        BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
        ReturnUrl = "https://app/payment/return"
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifyCallback_ValidatesHmacSignature(bool tamper)
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var query = BuildSignedQuery(options, tamper, out _);

        var result = await gateway.VerifyCallbackAsync(query);

        Assert.Equal(!tamper, result.IsValidSignature);
    }

    [Fact]
    public async Task VerifyCallback_WhenValid_ParsesFields()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);

        var result = await gateway.VerifyCallbackAsync(BuildSignedQuery(options, false, out _));

        Assert.True(result.IsValidSignature);
        Assert.True(result.Success);
        Assert.Equal(TxnRef, result.TransactionRef);
        Assert.Equal(100000m, result.Amount);
        Assert.Equal(5, result.OrderId);
    }

    [Fact]
    public async Task VerifyCallback_WhenResponseCodeNotSuccess_ReturnsNotSuccess()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var query = BuildSignedQuery(options, false, out _, new Dictionary<string, string>
        {
            ["vnp_ResponseCode"] = "24"
        });

        var result = await gateway.VerifyCallbackAsync(query);

        Assert.True(result.IsValidSignature);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreatePaymentAsync_SignsUrlWithHmacSha512()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var request = new PaymentRequest { OrderId = 5, Amount = 100000m, OrderInfo = "Thanh toan don hang 5" };

        var result = await gateway.CreatePaymentAsync(request, options.ReturnUrl, "127.0.0.1");

        Assert.True(result.Success);
        Assert.NotNull(result.RedirectUrl);
        Assert.Null(result.Error);

        var query = ParseQuery(new Uri(result.RedirectUrl!).Query);
        Assert.Equal("10000000", query["vnp_Amount"]);
        Assert.Equal("T", query["vnp_TmnCode"]);
        Assert.Equal(options.ReturnUrl, query["vnp_ReturnUrl"]);
        Assert.Equal("127.0.0.1", query["vnp_IpAddr"]);
        Assert.Equal("2.1.0", query["vnp_Version"]);
        Assert.Equal("pay", query["vnp_Command"]);
        Assert.Equal("VND", query["vnp_CurrCode"]);
        Assert.Equal(14, query["vnp_CreateDate"].Length);
        Assert.Equal(result.TransactionRef, query["vnp_TxnRef"]);
        Assert.StartsWith("5-", result.TransactionRef);

        var expected = Hmac(options.HashSecret, Canonical(
            query.Where(kv => kv.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))));
        Assert.Equal(expected, query["vnp_SecureHash"]);
    }

    [Fact]
    public async Task CreatePaymentAsync_StripsDiacriticsFromOrderInfo()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var request = new PaymentRequest { OrderId = 5, Amount = 1000m, OrderInfo = "Thanh toán đơn hàng 5" };

        var result = await gateway.CreatePaymentAsync(request, options.ReturnUrl, "127.0.0.1");

        var query = ParseQuery(new Uri(result.RedirectUrl!).Query);
        Assert.Equal("Thanh toan don hang 5", query["vnp_OrderInfo"]);
    }

    private static IQueryCollection BuildSignedQuery(
        VnPayOptions options,
        bool tamper,
        out string secureHash,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = TxnRef,
            ["vnp_ResponseCode"] = "00",
            ["vnp_Amount"] = AmountParam,
            ["vnp_TransactionStatus"] = "00",
            ["vnp_OrderInfo"] = "Thanh toan don hang",
            ["vnp_TmnCode"] = options.TmnCode
        };
        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        secureHash = Hmac(options.HashSecret, Canonical(parameters));
        if (tamper)
        {
            parameters["vnp_Amount"] = "20000000";
        }

        parameters["vnp_SecureHashType"] = "HmacSHA512";
        parameters["vnp_SecureHash"] = secureHash;

        return new QueryCollection(parameters.ToDictionary(
            kv => kv.Key,
            kv => new StringValues(kv.Value)));
    }

    private static Dictionary<string, string> ParseQuery(string queryString) =>
        queryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => WebUtility.UrlDecode(parts[0]),
                parts => WebUtility.UrlDecode(parts.Length > 1 ? parts[1] : string.Empty));

    private static string Canonical(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));

    private static string Hmac(string secret, string data) =>
        Convert.ToHexString(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
}
