using ECommerce.Web.Models.Entities.Enums;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Web.Services.Payments;

/// <summary>Khởi tạo lượt thanh toán cho đơn hàng và xử lý callback trả về từ cổng thanh toán.</summary>
public interface IPaymentService
{
    Task<PaymentInitResult> StartPaymentAsync(int orderId, PaymentMethod method, string ipAddress);

    Task<PaymentCallbackHandlingResult> HandleCallbackAsync(string provider, IQueryCollection query);
}
