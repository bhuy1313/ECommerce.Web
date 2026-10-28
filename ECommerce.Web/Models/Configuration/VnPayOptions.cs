namespace ECommerce.Web.Models.Configuration;

/// <summary>Thông tin cấu hình cổng thanh toán VNPay (TmnCode/HashSecret lấy từ env ở production).</summary>
public class VnPayOptions
{
    public string TmnCode { get; set; } = string.Empty;

    public string HashSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>Đủ cấu hình để khởi tạo giao dịch VNPay hay chưa.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TmnCode) &&
        !string.IsNullOrWhiteSpace(HashSecret) &&
        !string.IsNullOrWhiteSpace(ReturnUrl);
}
