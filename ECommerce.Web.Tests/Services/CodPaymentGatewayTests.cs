using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Web.Tests.Services;

public class CodPaymentGatewayTests
{
    [Fact]
    public async Task CreatePaymentAsync_ConfirmsImmediatelyWithoutRedirect()
    {
        var gateway = new CodPaymentGateway();
        var request = new PaymentRequest { OrderId = 5, Amount = 100000m, OrderInfo = "Thanh toan don hang 5" };

        var result = await gateway.CreatePaymentAsync(request, "https://app/payment/return", "127.0.0.1");

        Assert.Equal("Cod", gateway.Provider);
        Assert.True(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.Null(result.Error);
        Assert.Equal("COD-5", result.TransactionRef);
    }

    [Fact]
    public async Task CreatePaymentAsync_ForDifferentOrders_ProducesDifferentTransactionRefs()
    {
        var gateway = new CodPaymentGateway();

        var first = await gateway.CreatePaymentAsync(
            new PaymentRequest { OrderId = 5, Amount = 1000m, OrderInfo = "a" }, "https://app/payment/return", "127.0.0.1");
        var second = await gateway.CreatePaymentAsync(
            new PaymentRequest { OrderId = 6, Amount = 1000m, OrderInfo = "b" }, "https://app/payment/return", "127.0.0.1");

        Assert.NotEqual(first.TransactionRef, second.TransactionRef);
    }

    [Fact]
    public async Task VerifyCallbackAsync_ThrowsNotSupported()
    {
        var gateway = new CodPaymentGateway();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => gateway.VerifyCallbackAsync(new QueryCollection()));
    }
}
