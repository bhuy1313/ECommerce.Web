using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;

namespace ECommerce.Web.Services;

public interface IOrderService
{
    Task<(bool Success, string Message, int? OrderId)> CreateOrderFromCartAsync(string userId, ShippingDetails shipping, PaymentMethod method, string? couponCode = null);
    Task<IEnumerable<Order>> GetCustomerOrdersAsync(string userId);
    Task<Order?> GetOrderDetailsAsync(int orderId, string? userId = null);
    Task<(bool Success, string Message)> CancelOrderAsync(int orderId, string? userId = null);
    Task<(bool Success, string Message)> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
    Task<(bool Success, string Message)> UpdatePaymentStatusAsync(int orderId, PaymentStatus newStatus);
    Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null, string? search = null);
}
