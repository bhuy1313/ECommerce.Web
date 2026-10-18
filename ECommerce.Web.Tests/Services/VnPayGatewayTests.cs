using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Services.Payments;
using ECommerce.Web.Tests.Helpers;

namespace ECommerce.Web.Tests.Services;

public class VnPayGatewayTests
{
    private const string TxnRef = "5-1700000000000";
    private const decimal Amount = 100000m;

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
        var query = VnPaySignatureHelper.BuildCallbackQuery(
            options,
            TxnRef,
            Amount,
            secureHash: tamper ? VnPaySignatureHelper.WrongSignature : null);

        var result = await gateway.VerifyCallbackAsync(query);

        Assert.Equal(!tamper, result.IsValidSignature);
    }

    [Fact]
    public async Task VerifyCallback_WhenValid_ParsesFields()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);

        var result = await gateway.VerifyCallbackAsync(
            VnPaySignatureHelper.BuildCallbackQuery(options, TxnRef, Amount));

        Assert.True(result.IsValidSignature);
        Assert.True(result.Success);
        Assert.Equal(TxnRef, result.TransactionRef);
        Assert.Equal(Amount, result.Amount);
        Assert.Equal(5, result.OrderId);
    }

    [Fact]
    public async Task VerifyCallback_WhenResponseCodeNotSuccess_ReturnsNotSuccess()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var query = VnPaySignatureHelper.BuildCallbackQuery(options, TxnRef, Amount, responseCode: "24");

        var result = await gateway.VerifyCallbackAsync(query);

        Assert.True(result.IsValidSignature);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreatePaymentAsync_SignsUrlWithHmacSha512()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var request = new PaymentRequest { OrderId = 5, Amount = Amount, OrderInfo = "Thanh toan don hang 5" };

        var result = await gateway.CreatePaymentAsync(request, options.ReturnUrl, "127.0.0.1");

        Assert.True(result.Success);
        Assert.NotNull(result.RedirectUrl);
        Assert.Null(result.Error);

        var query = VnPaySignatureHelper.ParseQuery(new Uri(result.RedirectUrl!).Query);
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

        var expected = VnPaySignatureHelper.Sign(
            options.HashSecret,
            query.Where(kv => kv.Key is not ("vnp_SecureHash" or "vnp_SecureHashType")));
        Assert.Equal(expected, query["vnp_SecureHash"]);
    }

    [Fact]
    public async Task CreatePaymentAsync_StripsDiacriticsFromOrderInfo()
    {
        var options = Options();
        var gateway = new VnPayGateway(options);
        var request = new PaymentRequest { OrderId = 5, Amount = 1000m, OrderInfo = "Thanh toán đơn hàng 5" };

        var result = await gateway.CreatePaymentAsync(request, options.ReturnUrl, "127.0.0.1");

        var query = VnPaySignatureHelper.ParseQuery(new Uri(result.RedirectUrl!).Query);
        Assert.Equal("Thanh toan don hang 5", query["vnp_OrderInfo"]);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("   ", "secret")]
    [InlineData("T", "")]
    [InlineData("T", "   ")]
    public async Task CreatePaymentAsync_WhenCredentialsMissing_FailsInsteadOfRedirecting(string tmnCode, string hashSecret)
    {
        // Thiếu TmnCode/HashSecret thì URL sinh ra luôn bị VNPay từ chối ("Input data required"),
        // và chữ ký rỗng-secret thì ai cũng giả được. Không được đẩy khách sang một trang lỗi
        // của cổng thanh toán: phải trả về thất bại để tầng gọi dừng lại và báo cho người dùng.
        var options = Options();
        options.TmnCode = tmnCode;
        options.HashSecret = hashSecret;
        var gateway = new VnPayGateway(options);

        var result = await gateway.CreatePaymentAsync(
            new PaymentRequest { OrderId = 5, Amount = Amount, OrderInfo = "Thanh toan don hang 5" },
            options.ReturnUrl,
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task CreatePaymentAsync_WhenReturnUrlMissing_FailsInsteadOfRedirecting()
    {
        // vnp_ReturnUrl là bắt buộc: không có nó khách thanh toán xong sẽ không quay về được đâu.
        var options = Options();
        options.ReturnUrl = string.Empty;
        var gateway = new VnPayGateway(options);

        var result = await gateway.CreatePaymentAsync(
            new PaymentRequest { OrderId = 5, Amount = Amount, OrderInfo = "Thanh toan don hang 5" },
            string.Empty,
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task VerifyCallback_WhenCredentialsMissing_RejectsSignature()
    {
        // HashSecret rỗng nghĩa là chữ ký HMAC(empty-key) mà bất kỳ ai cũng tính được.
        // Chấp nhận nó sẽ biến callback thành đường ghi DB không cần xác thực.
        var configured = Options();
        var query = VnPaySignatureHelper.BuildCallbackQuery(configured, TxnRef, Amount);

        var unconfigured = Options();
        unconfigured.HashSecret = string.Empty;
        var gateway = new VnPayGateway(unconfigured);

        var result = await gateway.VerifyCallbackAsync(query);

        Assert.False(result.IsValidSignature);
    }
}
