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

        // Dedup theo (Provider, TransactionRef): COD sinh ref tất định "COD-{orderId}" nên lần gọi
        // thứ hai (double-click, hoặc nút "thanh toán lại") sẽ đụng unique index. VNPay mỗi lượt
        // sinh ref mới (kèm timestamp) nên vẫn tạo giao dịch + URL mới, đúng ngữ nghĩa.
        var transactionRef = result.TransactionRef ?? string.Empty;
        if (transactionRef.Length > 0
            && await _db.PaymentTransactions.AnyAsync(
                t => t.Provider == gateway.Provider && t.TransactionRef == transactionRef))
        {
            _logger.LogInformation(
                "Tái sử dụng lượt thanh toán {Provider} đã có cho đơn hàng #{OrderId} (ref {TransactionRef})",
                gateway.Provider, order.Id, transactionRef);

            return result;
        }

        var now = DateTime.UtcNow;
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            OrderId = order.Id,
            Provider = gateway.Provider,
            TransactionRef = transactionRef,
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

    public async Task<PaymentCallbackHandlingResult> HandleCallbackAsync(string provider, IQueryCollection query)
    {
        var gateway = FindGateway(provider);
        if (gateway == null)
        {
            return Failed(PaymentCallbackOutcome.ProviderUnsupported, $"Cổng thanh toán '{provider}' không được hỗ trợ.");
        }

        PaymentCallbackResult callback;
        try
        {
            callback = await gateway.VerifyCallbackAsync(query);
        }
        catch (NotSupportedException)
        {
            return Failed(PaymentCallbackOutcome.GatewayHasNoCallback, $"Cổng thanh toán '{provider}' không hỗ trợ callback.");
        }

        if (!callback.IsValidSignature)
        {
            _logger.LogWarning("Callback {Provider} có chữ ký không hợp lệ (ref {TransactionRef})",
                provider, callback.TransactionRef);
            return Failed(PaymentCallbackOutcome.InvalidSignature, "Chữ ký callback không hợp lệ.");
        }

        var transaction = await _db.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Provider == gateway.Provider && t.TransactionRef == callback.TransactionRef);

        if (transaction == null)
        {
            return Failed(PaymentCallbackOutcome.TransactionNotFound, "Không tìm thấy giao dịch tương ứng.");
        }

        // Idempotent: callback lặp lại (IPN retry / user refresh ReturnUrl) không xử lý lần hai.
        if (transaction.Status == PaymentTransactionStatus.Succeeded)
        {
            return Handled(PaymentCallbackOutcome.AlreadyProcessed, "Giao dịch đã được xử lý trước đó.", transaction.OrderId);
        }

        if (!callback.Success)
        {
            transaction.Status = PaymentTransactionStatus.Failed;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.RawResponse = BuildRawResponse(query);
            await _db.SaveChangesAsync();

            return Failed(PaymentCallbackOutcome.PaymentFailed, "Thanh toán không thành công theo phản hồi của cổng thanh toán.", transaction.OrderId);
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == transaction.OrderId);
        if (order == null)
        {
            return Failed(PaymentCallbackOutcome.OrderNotFound, "Không tìm thấy đơn hàng của giao dịch.", transaction.OrderId);
        }

        if (callback.Amount != order.TotalAmount)
        {
            _logger.LogWarning(
                "Callback {Provider} sai số tiền: nhận {CallbackAmount}, đơn hàng #{OrderId} là {OrderAmount}",
                provider, callback.Amount, order.Id, order.TotalAmount);
            return Failed(PaymentCallbackOutcome.AmountMismatch, "Số tiền thanh toán không khớp với đơn hàng.", order.Id);
        }

        // Đơn đã bị khách hủy (tồn kho đã được hoàn) nhưng tiền vẫn về sau đó.
        // KHÔNG được hồi sinh đơn: hàng đã trả lại kho, hồi sinh sẽ bán vượt tồn.
        // Chỉ ghi nhận đã thu tiền của đơn đã hủy để vận hành hoàn tiền thủ công.
        if (order.Status == OrderStatus.Cancelled)
        {
            var cancelledOrderStrategy = _db.Database.CreateExecutionStrategy();
            return await cancelledOrderStrategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    transaction.Status = PaymentTransactionStatus.Succeeded;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    transaction.RawResponse = BuildRawResponse(query);

                    // Giữ nguyên order.Status = Cancelled; chỉ đánh dấu khoản tiền cần hoàn.
                    order.PaymentStatus = PaymentStatus.RefundPending;

                    await _db.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    _logger.LogError(
                        "CẢNH BÁO HOÀN TIỀN: đơn hàng #{OrderId} đã bị hủy nhưng nhận được {Amount} qua {Provider} (ref {TransactionRef}) — cần hoàn tiền thủ công.",
                        order.Id, callback.Amount, provider, transaction.TransactionRef);

                    return Handled(
                        PaymentCallbackOutcome.PaidButCancelled,
                        $"Đơn hàng #{order.Id} đã bị hủy nhưng giao dịch đã được thu tiền. Bộ phận vận hành sẽ xử lý hoàn tiền.",
                        order.Id);
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi xử lý callback thanh toán cho đơn hàng đã hủy #{OrderId}", order.Id);
                    return Failed(PaymentCallbackOutcome.GatewayError, "Đã xảy ra lỗi khi xử lý thanh toán.");
                }
            });
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

                return Handled(PaymentCallbackOutcome.Confirmed, $"Đã xác nhận thanh toán cho đơn hàng #{order.Id}.", order.Id);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý callback thanh toán cho đơn hàng #{OrderId}", order.Id);
                return Failed(PaymentCallbackOutcome.GatewayError, "Đã xảy ra lỗi khi xử lý thanh toán.");
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

    private static PaymentCallbackHandlingResult Handled(PaymentCallbackOutcome outcome, string message, int? orderId) =>
        new() { Success = true, Outcome = outcome, Message = message, OrderId = orderId };

    private static PaymentCallbackHandlingResult Failed(PaymentCallbackOutcome outcome, string message, int? orderId = null) =>
        new() { Success = false, Outcome = outcome, Message = message, OrderId = orderId };
}
