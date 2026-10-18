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
