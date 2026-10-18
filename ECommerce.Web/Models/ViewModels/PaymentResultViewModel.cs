namespace ECommerce.Web.Models.ViewModels;

/// <summary>
/// Kết quả một lần cổng thanh toán gọi ngược về hệ thống, dùng cho trang Payment/Result.
/// </summary>
public class PaymentResultViewModel
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? OrderId { get; set; }
}
