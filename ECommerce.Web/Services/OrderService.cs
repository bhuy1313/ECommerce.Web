using ECommerce.Web.Data;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, int? OrderId)> CreateOrderFromCartAsync(string userId, ShippingDetails shipping)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            return (false, "Giỏ hàng của bạn đang trống.", null);
        }

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // 1. Kiểm tra toàn bộ sản phẩm và giới hạn tồn kho
                foreach (var item in cart.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Sản phẩm '{item.Product?.Name ?? "ID " + item.ProductId}' hiện không còn khả dụng.", (int?)null);
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Sản phẩm '{product.Name}' không đủ tồn kho (chỉ còn {product.StockQuantity}, bạn đặt {item.Quantity}).", (int?)null);
                    }
                }

                // 2. Trừ tồn kho và khởi tạo đơn hàng
                var order = new Order
                {
                    UserId = userId,
                    RecipientName = shipping.RecipientName.Trim(),
                    RecipientPhone = shipping.RecipientPhone.Trim(),
                    ShippingAddress = shipping.ShippingAddress.Trim(),
                    Note = string.IsNullOrWhiteSpace(shipping.Note) ? null : shipping.Note,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    TotalAmount = cart.Items.Sum(i => i.Quantity * i.UnitPrice)
                };

                foreach (var item in cart.Items)
                {
                    var product = products[item.ProductId];

                    // Trừ kho ATOMIC có điều kiện: 1 câu UPDATE duy nhất vừa kiểm tra vừa trừ.
                    // Hai transaction đồng thời chỉ có 1 bên thắng (bên còn lại affected == 0),
                    // nên stock không bao giờ âm dù precheck ở trên đã bị qua mặt bởi race.
                    var affected = await _db.Products
                        .Where(p => p.Id == product.Id && p.StockQuantity >= item.Quantity)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Quantity));

                    if (affected == 0)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Sản phẩm '{product.Name}' không đủ tồn kho (chỉ còn {product.StockQuantity}, bạn đặt {item.Quantity}).", (int?)null);
                    }

                    // ExecuteUpdate bỏ qua change tracker: detach instance đã stale để
                    // SaveChangesAsync bên dưới không ghi đè Products bằng giá trị cũ
                    // (ghi đè mù sẽ làm mất deduct của transaction khác = tái sinh race).
                    _db.Entry(product).State = EntityState.Detached;

                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }

                _db.Orders.Add(order);

                // 3. Làm trống giỏ hàng của người dùng
                _db.CartItems.RemoveRange(cart.Items);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Tạo đơn hàng #{OrderId} thành công cho User {UserId} với tổng tiền ${TotalAmount}",
                    order.Id, userId, order.TotalAmount);

                return (true, "Đặt hàng thành công!", order.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi xảy ra trong transaction tạo đơn hàng cho User {UserId}", userId);
                return (false, "Đã xảy ra lỗi trong quá trình xử lý đơn hàng. Vui lòng thử lại sau.", (int?)null);
            }
        });
    }

    public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(string userId)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Order?> GetOrderDetailsAsync(int orderId, string? userId = null)
    {
        var query = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(o => o.UserId == userId);
        }

        return await query.FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<(bool Success, string Message)> CancelOrderAsync(int orderId, string? userId = null)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || (!string.IsNullOrEmpty(userId) && order.UserId != userId))
        {
            return (false, "Không tìm thấy đơn hàng cần hủy.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return (false, "Đơn hàng này đã được hủy trước đó.");
        }

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Completed)
        {
            return (false, "Không thể hủy đơn hàng đang giao hoặc đã hoàn tất.");
        }

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Hoàn lại tồn kho cho từng sản phẩm trong đơn
                foreach (var item in order.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                order.Status = OrderStatus.Cancelled;
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    order.PaymentStatus = PaymentStatus.Refunded;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Hủy đơn hàng #{OrderId} và hoàn tồn kho thành công.", orderId);
                return (true, $"Đã hủy đơn hàng #{orderId} và hoàn lại số lượng tồn kho thành công.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi hủy đơn hàng #{OrderId}", orderId);
                return (false, "Đã xảy ra lỗi khi hủy đơn hàng.");
            }
        });
    }

    public async Task<(bool Success, string Message)> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return (false, "Không tìm thấy đơn hàng.");
        }

        if (order.Status == newStatus)
        {
            return (true, "Trạng thái đơn hàng không thay đổi.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return (false, "Không thể thay đổi trạng thái của đơn hàng đã bị hủy.");
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            return await CancelOrderAsync(orderId, userId: null);
        }

        order.Status = newStatus;
        await _db.SaveChangesAsync();

        return (true, $"Đã cập nhật trạng thái đơn hàng #{orderId} thành '{newStatus}'.");
    }

    public async Task<(bool Success, string Message)> UpdatePaymentStatusAsync(int orderId, PaymentStatus newStatus)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null)
        {
            return (false, "Không tìm thấy đơn hàng.");
        }

        order.PaymentStatus = newStatus;
        await _db.SaveChangesAsync();

        return (true, $"Đã cập nhật trạng thái thanh toán đơn hàng #{orderId} thành '{newStatus}'.");
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null, string? search = null)
    {
        var query = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            if (int.TryParse(trimmed.Replace("#", ""), out var searchId))
            {
                query = query.Where(o => o.Id == searchId || (o.User != null && o.User.Email != null && o.User.Email.Contains(trimmed)));
            }
            else
            {
                query = query.Where(o => o.User != null && o.User.Email != null && o.User.Email.Contains(trimmed));
            }
        }

        return await query.OrderByDescending(o => o.OrderDate).ToListAsync();
    }
}
