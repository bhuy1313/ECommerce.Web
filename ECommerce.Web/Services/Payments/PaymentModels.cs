namespace ECommerce.Web.Services.Payments;

/// <summary>Yêu cầu khởi tạo một lượt thanh toán.</summary>
public class PaymentRequest
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string OrderInfo { get; set; } = string.Empty;
}

/// <summary>Kết quả khởi tạo thanh toán: hoặc có URL chuyển hướng, hoặc xác nhận ngay (COD).</summary>
public class PaymentInitResult
{
    public bool Success { get; set; }

    public string? RedirectUrl { get; set; }

    public string? TransactionRef { get; set; }

    public string? Error { get; set; }
}

/// <summary>Kết quả xác thực callback từ cổng thanh toán.</summary>
public class PaymentCallbackResult
{
    public bool Success { get; set; }

    public string TransactionRef { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public bool IsValidSignature { get; set; }

    public int? OrderId { get; set; }
}

/// <summary>
/// Lý do cuối cùng mà <see cref="IPaymentService.HandleCallbackAsync"/> đưa ra cho một callback.
/// Tồn tại để tầng gọi (PaymentController) ánh xạ sang mã RspCode của cổng thanh toán bằng
/// cấu trúc thay vì dò chuỗi con trong câu thông báo tiếng Việt.
/// </summary>
public enum PaymentCallbackOutcome
{
    /// <summary>Chữ ký hợp lệ, số tiền khớp: đơn hàng đã được ghi nhận thanh toán.</summary>
    Confirmed,

    /// <summary>Callback lặp lại cho một giao dịch đã xử lý thành công trước đó.</summary>
    AlreadyProcessed,

    /// <summary>Đã thu được tiền nhưng đơn hàng đã bị hủy: cần hoàn tiền thủ công.</summary>
    PaidButCancelled,

    /// <summary>Không tìm thấy đơn hàng của giao dịch.</summary>
    OrderNotFound,

    /// <summary>Không tìm thấy giao dịch tương ứng với mã tham chiếu trong callback.</summary>
    TransactionNotFound,

    /// <summary>Chữ ký HMAC của callback không hợp lệ.</summary>
    InvalidSignature,

    /// <summary>Số tiền trong callback không khớp với đơn hàng.</summary>
    AmountMismatch,

    /// <summary>Cổng thanh toán trả về kết quả thất bại cho giao dịch.</summary>
    PaymentFailed,

    /// <summary>Cổng thanh toán không hỗ trợ callback (ví dụ COD).</summary>
    GatewayHasNoCallback,

    /// <summary>Provider trong URL không có cổng thanh toán tương ứng.</summary>
    ProviderUnsupported,

    /// <summary>Lỗi không mong đợi khi xử lý callback.</summary>
    GatewayError
}

/// <summary>
/// Kết quả xử lý một callback: <see cref="Success"/> cho biết callback đã được xử lý trọn vẹn
/// (kể cả khi phải hoàn tiền thủ công), <see cref="Outcome"/> cho biết chính xác vì sao.
/// </summary>
public class PaymentCallbackHandlingResult
{
    public bool Success { get; set; }

    public PaymentCallbackOutcome Outcome { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? OrderId { get; set; }
}
