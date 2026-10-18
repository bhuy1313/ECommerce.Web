using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

/// <summary>
/// Cổng thanh toán gọi ngược vào hệ thống qua hai endpoint công khai:
/// GET  Payment/Return — trình duyệt khách quay về sau khi thanh toán;
/// GET/POST Payment/Ipn — VNPay gọi server-to-server để xác nhận kết quả.
/// Cả hai đều PHẢI đi qua cùng một luồng kiểm tra chữ ký của IPaymentService;
/// IPN phải trả đúng mã RspCode theo spec VNPay để cổng thanh toán ngừng retry.
/// </summary>
public class PaymentControllerTests
{
    private static PaymentController CreateController(Mock<IPaymentService> paymentService)
    {
        return new PaymentController(
            paymentService.Object,
            new Mock<INotyfService>().Object,
            Mock.Of<ILogger<PaymentController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static Mock<IPaymentService> ServiceReturning(PaymentCallbackOutcome outcome, string message, int? orderId)
    {
        var paymentService = new Mock<IPaymentService>();
        paymentService
            .Setup(s => s.HandleCallbackAsync(It.IsAny<string>(), It.IsAny<IQueryCollection>()))
            .ReturnsAsync(new PaymentCallbackHandlingResult
            {
                Success = outcome is PaymentCallbackOutcome.Confirmed
                    or PaymentCallbackOutcome.AlreadyProcessed
                    or PaymentCallbackOutcome.PaidButCancelled,
                Outcome = outcome,
                Message = message,
                OrderId = orderId
            });
        return paymentService;
    }

    private static string RspCodeOf(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        var rspCode = json.Value?.GetType().GetProperty("RspCode")?.GetValue(json.Value);
        Assert.NotNull(rspCode);
        return (string)rspCode!;
    }

    [Fact]
    public void Ipn_IsReachableWithoutAuthentication()
    {
        // VNPay gọi IPN server-to-server: không có cookie, không có antiforgery token.
        // Bất kỳ [Authorize] nào ở đây cũng biến IPN thành 401/302 và VNPay sẽ retry vô ích.
        Assert.Null(typeof(PaymentController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).FirstOrDefault());

        var ipn = typeof(PaymentController).GetMethod(nameof(PaymentController.Ipn));
        Assert.NotNull(ipn);
        Assert.Null(ipn!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).FirstOrDefault());
    }

    [Fact]
    public async Task Ipn_WhenCallbackSucceeds_ReturnsRspCode00()
    {
        var paymentService = ServiceReturning(PaymentCallbackOutcome.Confirmed, "Xác nhận thanh toán thành công.", 42);
        var controller = CreateController(paymentService);

        var result = await controller.Ipn();

        Assert.Equal("00", RspCodeOf(result));
        paymentService.Verify(
            s => s.HandleCallbackAsync("VnPay", It.IsAny<IQueryCollection>()),
            Times.Once);
    }

    [Theory]
    [InlineData(PaymentCallbackOutcome.Confirmed)]
    [InlineData(PaymentCallbackOutcome.AlreadyProcessed)]
    [InlineData(PaymentCallbackOutcome.PaidButCancelled)]
    public async Task Ipn_WhenHandledOutcome_ReturnsRspCode00(PaymentCallbackOutcome outcome)
    {
        // VNPay phải ngừng retry khi callback đã được xử lý trọn vẹn — kể cả trường hợp
        // PaidButCancelled, vì tiền đã thu được và hệ thống đã ghi nhận để hoàn tiền thủ công.
        var controller = CreateController(ServiceReturning(outcome, "Đã xử lý.", 42));

        var result = await controller.Ipn();

        Assert.Equal("00", RspCodeOf(result));
    }

    [Fact]
    public async Task Ipn_WhenSignatureInvalid_ReturnsRspCode97()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.InvalidSignature,
            "Chữ ký không hợp lệ.",
            null));

        var result = await controller.Ipn();

        Assert.Equal("97", RspCodeOf(result));
    }

    [Fact]
    public async Task Ipn_WhenTransactionsNotFound_ReturnsRspCode01()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.TransactionNotFound,
            "Không tìm thấy giao dịch tương ứng với đơn hàng.",
            null));

        var result = await controller.Ipn();

        Assert.Equal("01", RspCodeOf(result));
    }

    [Fact]
    public async Task Ipn_WhenOrderNotFound_ReturnsRspCode01()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.OrderNotFound,
            "Không tìm thấy đơn hàng của giao dịch.",
            7));

        var result = await controller.Ipn();

        Assert.Equal("01", RspCodeOf(result));
    }

    [Fact]
    public async Task Ipn_WhenAmountMismatch_ReturnsRspCode04()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.AmountMismatch,
            "Số tiền thanh toán không khớp với giá trị đơn hàng.",
            7));

        var result = await controller.Ipn();

        Assert.Equal("04", RspCodeOf(result));
    }

    [Theory]
    [InlineData(PaymentCallbackOutcome.PaymentFailed)]
    [InlineData(PaymentCallbackOutcome.ProviderUnsupported)]
    [InlineData(PaymentCallbackOutcome.GatewayHasNoCallback)]
    [InlineData(PaymentCallbackOutcome.GatewayError)]
    public async Task Ipn_WhenOutcomeIsUnrecognised_ReturnsRspCode99(PaymentCallbackOutcome outcome)
    {
        var controller = CreateController(ServiceReturning(
            outcome,
            "Cổng thanh toán báo lỗi không xác định.",
            7));

        var result = await controller.Ipn();

        Assert.Equal("99", RspCodeOf(result));
    }

    [Fact]
    public async Task Ipn_SerialisesPascalCaseKeysForVnPay()
    {
        // Spec IPN của VNPay dùng đúng hai khóa "RspCode" và "Message". Chính sách JSON mặc định
        // của ASP.NET Core là camelCase, nên nếu không ghim tên thì response thành "rspCode"
        // — khác hợp đồng với cổng thanh toán.
        var controller = CreateController(ServiceReturning(PaymentCallbackOutcome.Confirmed, "ok", 1));

        var result = await controller.Ipn();

        var json = Assert.IsType<JsonResult>(result);
        var payload = System.Text.Json.JsonSerializer.Serialize(
            json.Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"RspCode\":", payload);
        Assert.DoesNotContain("\"rspCode\":", payload);
        Assert.Contains("\"Message\":", payload);
    }

    [Fact]
    public async Task Return_WhenCallbackSucceeds_RedirectsToOrderDetails()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.Confirmed,
            "Thanh toán thành công.",
            42));

        var result = await controller.Return();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal("Orders", redirect.ControllerName);
        Assert.Equal(42, redirect.RouteValues?["id"]);
    }

    [Fact]
    public async Task Return_WhenCallbackFails_RendersResultViewWithFailure()
    {
        var controller = CreateController(ServiceReturning(
            PaymentCallbackOutcome.InvalidSignature,
            "Chữ ký không hợp lệ.",
            null));

        var result = await controller.Return();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Result", view.ViewName);
        var model = Assert.IsType<PaymentResultViewModel>(view.Model);
        Assert.False(model.Success);
        Assert.Equal("Chữ ký không hợp lệ.", model.Message);
        Assert.Null(model.OrderId);
    }
}
