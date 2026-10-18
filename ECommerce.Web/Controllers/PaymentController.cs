using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace ECommerce.Web.Controllers;

/// <summary>
/// Endpoint công khai để cổng thanh toán gọi ngược về (VNPay). Không có [Authorize]:
/// VNPay không thể đăng nhập, và mọi yêu cầu ở đây đều được xác thực bằng chữ ký HMAC
/// bên trong <see cref="IPaymentService"/>. Vì cùng lý do, không dùng antiforgery token.
/// </summary>
public class PaymentController : Controller
{
    /// <summary>
    /// Mã phản hồi theo spec IPN của VNPay. VNPay CHỈ ngừng retry khi nhận được mã
    /// thuộc nhóm này; mọi mã khác (hoặc lỗi HTTP) đều bị coi là "chưa xác nhận" và
    /// gọi lại tối đa 10 lần, nên việc phân loại ở đây có ý nghĩa vận hành thật.
    /// </summary>
    private const string RspSuccess = "00";
    private const string RspOrderNotFound = "01";
    private const string RspInvalidChecksum = "97";
    private const string RspInvalidAmount = "04";
    private const string RspUnknown = "99";

    private readonly IPaymentService _paymentService;
    private readonly INotyfService _notyf;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService,
        INotyfService notyf,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _notyf = notyf;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Return()
    {
        var result = await _paymentService.HandleCallbackAsync("VnPay", Request.Query);

        if (result.Success && result.OrderId.HasValue && result.Outcome != PaymentCallbackOutcome.PaidButCancelled)
        {
            _notyf.Success(result.Message);
            return RedirectToAction(nameof(OrdersController.Details), "Orders", new { id = result.OrderId.Value });
        }

        // Tiền đã thu nhưng đơn đã bị hủy: vẫn đưa khách về đơn hàng để thấy trạng thái
        // "Chờ hoàn tiền", nhưng dùng thông báo trung tính thay vì toast xanh "thành công".
        if (result.Outcome == PaymentCallbackOutcome.PaidButCancelled && result.OrderId.HasValue)
        {
            _notyf.Information(result.Message);
            return RedirectToAction(nameof(OrdersController.Details), "Orders", new { id = result.OrderId.Value });
        }

        _logger.LogWarning("VNPay return thất bại cho đơn {OrderId}: {Message}", result.OrderId, result.Message);
        _notyf.Error(result.Message);
        return View("Result", new PaymentResultViewModel
        {
            Success = result.Success,
            Message = result.Message,
            OrderId = result.OrderId
        });
    }

    /// <summary>
    /// IPN (Instant Payment Notification): VNPay gọi trực tiếp server-to-server bằng GET.
    /// Nhận cả POST để có thể kiểm thử/đối phó cấu hình khác nhau của cổng thanh toán.
    /// </summary>
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Ipn()
    {
        var result = await _paymentService.HandleCallbackAsync("VnPay", Request.Query);

        var rspCode = ResolveRspCode(result.Outcome);

        if (!result.Success)
        {
            _logger.LogWarning("VNPay IPN thất bại ({RspCode}) cho đơn {OrderId}: {Message}", rspCode, result.OrderId, result.Message);
        }

        return Json(new IpnResponse { RspCode = rspCode, Message = result.Message });
    }

    /// <summary>
    /// Hình dạng phản hồi IPN mà VNPay yêu cầu: đúng hai khóa <c>RspCode</c> và <c>Message</c>.
    /// Phải ghim tên bằng <see cref="JsonPropertyNameAttribute"/> vì chính sách JSON mặc định của
    /// ASP.NET Core là camelCase, sẽ biến chúng thành <c>rspCode</c>/<c>message</c>.
    /// </summary>
    private sealed class IpnResponse
    {
        [JsonPropertyName("RspCode")]
        public string RspCode { get; init; } = string.Empty;

        [JsonPropertyName("Message")]
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Ánh xạ kết quả xử lý callback sang mã RspCode của VNPay. Ánh xạ này là cấu trúc
    /// (enum) chứ không dò câu chữ, nên đổi thông báo tiếng Việt trong PaymentService
    /// không thể âm thầm đổi mã trả về cho cổng thanh toán.
    /// </summary>
    private static string ResolveRspCode(PaymentCallbackOutcome outcome) => outcome switch
    {
        PaymentCallbackOutcome.Confirmed
            or PaymentCallbackOutcome.AlreadyProcessed
            or PaymentCallbackOutcome.PaidButCancelled => RspSuccess,

        PaymentCallbackOutcome.OrderNotFound
            or PaymentCallbackOutcome.TransactionNotFound => RspOrderNotFound,

        PaymentCallbackOutcome.InvalidSignature => RspInvalidChecksum,

        PaymentCallbackOutcome.AmountMismatch => RspInvalidAmount,

        PaymentCallbackOutcome.ProviderUnsupported
            or PaymentCallbackOutcome.GatewayHasNoCallback
            or PaymentCallbackOutcome.PaymentFailed
            or PaymentCallbackOutcome.GatewayError => RspUnknown,

        _ => RspUnknown,
    };
}
