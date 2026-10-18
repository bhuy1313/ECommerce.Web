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

    /// <summary>
    /// Ánh xạ thông điệp của PaymentService sang mã RspCode.
    /// LƯU Ý: IPaymentService.HandleCallbackAsync chỉ trả về (Success, Message, OrderId),
    /// không trả về lý do thất bại dạng cấu trúc, nên phải dò chuỗi con trong Message.
    /// Các chuỗi dưới đây là những thông điệp ổn định đang được PaymentServiceTests khẳng định;
    /// nếu đổi câu chữ trong PaymentService phải cập nhật bảng này.
    /// </summary>
    private static readonly (string Fragment, string Code)[] RspCodeByMessage =
    {
        ("chữ ký", RspInvalidChecksum),
        ("không khớp", RspInvalidAmount),
        ("không tìm thấy", RspOrderNotFound),
    };

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
        var (success, message, orderId) = await _paymentService.HandleCallbackAsync("VnPay", Request.Query);

        if (success && orderId.HasValue)
        {
            _notyf.Success(message);
            return RedirectToAction(nameof(OrdersController.Details), "Orders", new { id = orderId.Value });
        }

        _logger.LogWarning("VNPay return thất bại cho đơn {OrderId}: {Message}", orderId, message);
        _notyf.Error(message);
        return View("Result", new PaymentResultViewModel
        {
            Success = success,
            Message = message,
            OrderId = orderId
        });
    }

    /// <summary>
    /// IPN (Instant Payment Notification): VNPay gọi trực tiếp server-to-server bằng GET.
    /// Nhận cả POST để có thể kiểm thử/đối phó cấu hình khác nhau của cổng thanh toán.
    /// </summary>
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Ipn()
    {
        var (success, message, orderId) = await _paymentService.HandleCallbackAsync("VnPay", Request.Query);

        var rspCode = success ? RspSuccess : ResolveRspCode(message);

        if (!success)
        {
            _logger.LogWarning("VNPay IPN thất bại ({RspCode}) cho đơn {OrderId}: {Message}", rspCode, orderId, message);
        }

        return Json(new IpnResponse { RspCode = rspCode, Message = message });
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

    private static string ResolveRspCode(string message)
    {
        foreach (var (fragment, code) in RspCodeByMessage)
        {
            if (message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return RspUnknown;
    }
}
