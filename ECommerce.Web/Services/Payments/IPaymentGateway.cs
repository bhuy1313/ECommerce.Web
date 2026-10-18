using Microsoft.AspNetCore.Http;

namespace ECommerce.Web.Services.Payments;

/// <summary>Cổng thanh toán: khởi tạo lượt thanh toán và xác thực callback trả về.</summary>
public interface IPaymentGateway
{
    string Provider { get; }

    Task<PaymentInitResult> CreatePaymentAsync(PaymentRequest request, string returnUrl, string ipAddress);

    Task<PaymentCallbackResult> VerifyCallbackAsync(IQueryCollection query);
}
