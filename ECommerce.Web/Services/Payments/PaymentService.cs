using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services.Payments;

/// <summary>
/// Điều phối thanh toán: khởi tạo giao dịch ở trạng thái Pending cho phương thức được chọn và
/// xử lý callback idempotent (chữ ký hợp lệ + đúng số tiền mới chuyển đơn sang Paid/Processing).
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ApplicationDbContext db, IEnumerable<IPaymentGateway> gateways, ILogger<PaymentService> logger)
    {
        _db = db;
        _gateways = gateways;
        _logger = logger;
    }

    public async Task<PaymentInitResult> StartPaymentAsync(int orderId, PaymentMethod method, string ipAddress)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null)
        {
            return Failure("Không tìm thấy đơn hàng.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return Failure("Đơn hàng đã được thanh toán.");
        }

        var gateway = FindGateway(method.ToString());
        if (gateway == null)
        {
            return Failure($"Phương thức thanh toán '{method}' không được hỗ trợ.");
        }

        // returnUrl rỗng: mỗi cổng tự dùng ReturnUrl trong cấu hình của nó.
        var result = await gateway.CreatePaymentAsync(
            new PaymentRequest
            {
                OrderId = order.Id,
                Amount = order.TotalAmount,
                OrderInfo = $"Thanh toan don hang {order.Id}"
            },
            string.Empty,
            ipAddress);

        if (!result.Success)
        {
            return result;
        }

        var now = DateTime.UtcNow;
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            OrderId = order.Id,
            Provider = gateway.Provider,
            TransactionRef = result.TransactionRef ?? string.Empty,
            Amount = order.TotalAmount,
            Status = PaymentTransactionStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Khởi tạo thanh toán {Provider} cho đơn hàng #{OrderId} (ref {TransactionRef})",
            gateway.Provider, order.Id, result.TransactionRef);

        return result;
    }

    public async Task<(bool Success, string Message, int? OrderId)> HandleCallbackAsync(string provider, IQueryCollection query)
    {
        var gateway = FindGateway(provider);
        if (gateway == null)
        {
            return (false, $"Cổng thanh toán '{provider}' không được hỗ trợ.", null);
        }

        PaymentCallbackResult callback;
        try
        {
            callback = await gateway.VerifyCallbackAsync(query);
        }
        catch (NotSupportedException)
        {
            return (false, $"Cổng thanh toán '{provider}' không hỗ trợ callback.", null);
        }

        if (!callback.IsValidSignature)
        {
            _logger.LogWarning("Callback {Provider} có chữ ký không hợp lệ (ref {TransactionRef})",
                provider, callback.TransactionRef);
            return (false, "Chữ ký callback không hợp lệ.", null);
        }

        var transaction = await _db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Provider == gateway.Provider && t.TransactionRef == callback.TransactionRef);

        if (transaction == null)
        {
            return (false, "Không tìm thấy giao dịch tương ứng.", null);
        }

        // Idempotent: callback lặp lại (IPN retry / user refresh ReturnUrl) không xử lý lần hai.
        if (transaction.Status == PaymentTransactionStatus.Succeeded)
        {
            return (true, "Giao dịch đã được xử lý trước đó.", transaction.OrderId);
        }

        if (!callback.Success)
        {
            transaction.Status = PaymentTransactionStatus.Failed;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.RawResponse = BuildRawResponse(query);
            await _db.SaveChangesAsync();

            return (false, "Thanh toán không thành công theo phản hồi của cổng thanh toán.", transaction.OrderId);
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == transaction.OrderId);
        if (order == null)
        {
            return (false, "Không tìm thấy đơn hàng của giao dịch.", transaction.OrderId);
        }

        if (callback.Amount != order.TotalAmount)
        {
            _logger.LogWarning(
                "Callback {Provider} sai số tiền: nhận {CallbackAmount}, đơn hàng #{OrderId} là {OrderAmount}",
                provider, callback.Amount, order.Id, order.TotalAmount);
            return (false, "Số tiền thanh toán không khớp với đơn hàng.", order.Id);
        }

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _db.Database.BeginTransactionAsync();
            try
            {
                transaction.Status = PaymentTransactionStatus.Succeeded;
                transaction.UpdatedAt = DateTime.UtcNow;
                transaction.RawResponse = BuildRawResponse(query);

                order.PaymentStatus = PaymentStatus.Paid;
                order.Status = OrderStatus.Processing;

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Đơn hàng #{OrderId} đã thanh toán qua {Provider} (ref {TransactionRef})",
                    order.Id, provider, transaction.TransactionRef);

                return (true, $"Đã xác nhận thanh toán cho đơn hàng #{order.Id}.", (int?)order.Id);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý callback thanh toán cho đơn hàng #{OrderId}", order.Id);
                return (false, "Đã xảy ra lỗi khi xử lý thanh toán.", (int?)null);
            }
        });
    }

    private IPaymentGateway? FindGateway(string provider) =>
        _gateways.FirstOrDefault(gateway =>
            string.Equals(gateway.Provider, provider, StringComparison.OrdinalIgnoreCase));

    private static string BuildRawResponse(IQueryCollection query) =>
        string.Join("&", query
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Key}={parameter.Value}"));

    private static PaymentInitResult Failure(string error) => new() { Success = false, Error = error };
}
