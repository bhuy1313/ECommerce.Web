using Microsoft.AspNetCore.Http;

namespace ECommerce.Web.Services.Payments;

/// <summary>
/// Thanh toán khi nhận hàng (COD): xác nhận ngay khi đặt, không chuyển hướng và không có callback.
/// </summary>
public class CodPaymentGateway : IPaymentGateway
{
    public string Provider => "Cod";

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentRequest request, string returnUrl, string ipAddress) =>
        Task.FromResult(new PaymentInitResult
        {
            Success = true,
            RedirectUrl = null,
            TransactionRef = $"COD-{request.OrderId}"
        });

    public Task<PaymentCallbackResult> VerifyCallbackAsync(IQueryCollection query) =>
        throw new NotSupportedException("COD không có callback từ cổng thanh toán.");
}
