using ECommerce.Web.Models.Entities.Enums;

namespace ECommerce.Web.Models.Entities;

/// <summary>Một lượt giao dịch với cổng thanh toán; cặp (Provider, TransactionRef) là duy nhất để chống xử lý trùng callback.</summary>
public class PaymentTransaction
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string TransactionRef { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
