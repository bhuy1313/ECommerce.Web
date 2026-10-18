namespace ECommerce.Web.Models.Entities.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Refunded = 3,

    /// <summary>
    /// Đã thu tiền nhưng khoản hoàn chưa được thực hiện (đơn bị hủy sau khi thanh toán).
    /// Chỉ chuyển sang <see cref="Refunded"/> khi admin xác nhận đã hoàn tiền.
    /// </summary>
    RefundPending = 4
}