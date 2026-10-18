using ECommerce.Web.Data;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Services;

public class OrderServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILogger<OrderService>> _loggerMock;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<OrderService>>();
        _orderService = new OrderService(_db, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static ShippingDetails Ship() => new() { RecipientName = "Test", RecipientPhone = "0900000000", ShippingAddress = "Test Address" };

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenCartEmpty_ReturnsFailure()
    {
        // Arrange
        var userId = "user-empty-cart";

        // Act
        var result = await _orderService.CreateOrderFromCartAsync(userId, Ship());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("trống", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.OrderId);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_PersistsShippingDetails()
    {
        // Arrange
        var userId = "user-ship";
        var product = new Product { Name = "Mouse", Price = 25m, StockQuantity = 5, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        var cart = new Cart { UserId = userId };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1, UnitPrice = 25m });
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        var shipping = new ShippingDetails
        {
            RecipientName = "Tran Thi B",
            RecipientPhone = "0912345678",
            ShippingAddress = "1 Nguyen Hue, Q1",
            Note = "Gọi trước khi giao"
        };

        // Act
        var result = await _orderService.CreateOrderFromCartAsync(userId, shipping);

        // Assert
        Assert.True(result.Success);
        var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == result.OrderId);
        Assert.Equal("Tran Thi B", order.RecipientName);
        Assert.Equal("0912345678", order.RecipientPhone);
        Assert.Equal("1 Nguyen Hue, Q1", order.ShippingAddress);
        Assert.Equal("Gọi trước khi giao", order.Note);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenStockSufficient_DeductsStockAndCalculatesTotalCorrectly()
    {
        // Arrange
        var userId = "user-valid-order";
        var category = new Category { Name = "Tech", Description = "Tech items", IsActive = true };
        var product1 = new Product { Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 10, Category = category, IsActive = true };
        var product2 = new Product { Name = "USB-C Cable", Price = 10.00m, StockQuantity = 20, Category = category, IsActive = true };
        _db.Categories.Add(category);
        _db.Products.AddRange(product1, product2);
        await _db.SaveChangesAsync();

        var cart = new Cart { UserId = userId };
        cart.Items.Add(new CartItem { ProductId = product1.Id, Quantity = 2, UnitPrice = 25.00m });
        cart.Items.Add(new CartItem { ProductId = product2.Id, Quantity = 3, UnitPrice = 10.00m });
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        // Act
        var result = await _orderService.CreateOrderFromCartAsync(userId, Ship());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OrderId);

        // Verify stock deducted (AsNoTracking: đọc trạng thái DB thực, vì RemoveRange
        // CartItems phía service re-attach Product navs stale vào tracker cùng context)
        var updatedProduct1 = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == product1.Id);
        var updatedProduct2 = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == product2.Id);
        Assert.Equal(8, updatedProduct1.StockQuantity); // 10 - 2
        Assert.Equal(17, updatedProduct2.StockQuantity); // 20 - 3

        // Verify order created with correct calculation: 2 * 25 + 3 * 10 = 80
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result.OrderId);
        Assert.NotNull(order);
        Assert.Equal(80.00m, order!.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);

        // Verify cart is cleared
        var updatedCart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(updatedCart);
        Assert.Empty(updatedCart!.Items);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenStockInsufficient_RollsBackAndFailsSafely()
    {
        // Arrange
        var userId = "user-stock-shortage";
        var category = new Category { Name = "Tech", Description = "Tech items", IsActive = true };
        var product = new Product { Name = "Rare Item", Price = 100.00m, StockQuantity = 2, Category = category, IsActive = true };
        _db.Categories.Add(category);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cart = new Cart { UserId = userId };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 5, UnitPrice = 100.00m }); // Demand 5 > Stock 2
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        // Act
        var result = await _orderService.CreateOrderFromCartAsync(userId, Ship());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("không đủ tồn kho", result.Message);
        Assert.Null(result.OrderId);

        // Verify stock was NOT deducted (anti-negative stock safeguard)
        var freshProduct = await _db.Products.FindAsync(product.Id);
        Assert.Equal(2, freshProduct!.StockQuantity);

        // Verify no order was saved
        var ordersCount = await _db.Orders.CountAsync();
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_WhenStockExactlyMatches_DeductsToZeroAndSecondOrderFails()
    {
        // Arrange: biên stock = 1, hai user cùng nhắm 1 sản phẩm (mô phỏng tranh chấp stock cuối)
        var product = new Product { Name = "Last Item", Price = 50.00m, StockQuantity = 1, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cartA = new Cart { UserId = "user-race-a" };
        cartA.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1, UnitPrice = 50.00m });
        var cartB = new Cart { UserId = "user-race-b" };
        cartB.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1, UnitPrice = 50.00m });
        _db.Carts.AddRange(cartA, cartB);
        await _db.SaveChangesAsync();

        // Act
        var first = await _orderService.CreateOrderFromCartAsync("user-race-a", Ship());
        var second = await _orderService.CreateOrderFromCartAsync("user-race-b", Ship());

        // Assert: đúng 1 đơn thắng, stock về 0 và không bao giờ âm, đơn thua báo hết hàng
        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("không đủ tồn kho", second.Message);
        Assert.Null(second.OrderId);

        var freshProduct = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
        Assert.Equal(0, freshProduct.StockQuantity);

        var ordersCount = await _db.Orders.CountAsync();
        Assert.Equal(1, ordersCount);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderPending_RefundsStockAndSetsCancelledStatus()
    {
        // Arrange
        var userId = "user-cancel-order";
        var product = new Product { Name = "Headphones", Price = 50.00m, StockQuantity = 5, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = 100.00m
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 2, UnitPrice = 50.00m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.CancelOrderAsync(order.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("thành công", message);

        var updatedOrder = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Cancelled, updatedOrder!.Status);

        // Verify stock was refunded (+2)
        var updatedProduct = await _db.Products.FindAsync(product.Id);
        Assert.Equal(7, updatedProduct!.StockQuantity); // 5 + 2 = 7
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderPaid_SetsRefunded()
    {
        // Arrange
        var userId = "user-refund-order";
        var product = new Product { Name = "Refundable", Price = 100.00m, StockQuantity = 3, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 100.00m
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 100.00m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.CancelOrderAsync(order.Id, userId);

        // Assert
        Assert.True(success);
        Assert.Contains("thành công", message);

        var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Cancelled, updatedOrder.Status);
        Assert.Equal(PaymentStatus.Refunded, updatedOrder.PaymentStatus);

        var updatedProduct = await _db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        Assert.Equal(4, updatedProduct.StockQuantity); // 3 + 1
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Completed)]
    public async Task CancelOrderAsync_WhenOrderShippedOrCompleted_DisallowsCancellation(OrderStatus nonCancellableStatus)
    {
        // Arrange
        var userId = "user-cannot-cancel";
        var product = new Product { Name = "Smart Watch", Price = 200.00m, StockQuantity = 10, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = new Order
        {
            UserId = userId,
            Status = nonCancellableStatus,
            PaymentStatus = PaymentStatus.Paid,
            TotalAmount = 200.00m
        };
        order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1, UnitPrice = 200.00m });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.CancelOrderAsync(order.Id, userId);

        // Assert
        Assert.False(success);
        Assert.Contains("Không thể hủy", message);

        // Stock remains unchanged
        var updatedProduct = await _db.Products.FindAsync(product.Id);
        Assert.Equal(10, updatedProduct!.StockQuantity);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderAlreadyCancelled_ReturnsFailure()
    {
        // Arrange
        var userId = "user-double-cancel";
        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Cancelled,
            TotalAmount = 50.00m
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.CancelOrderAsync(order.Id, userId);

        // Assert
        Assert.False(success);
        Assert.Contains("đã được hủy trước đó", message);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WhenValidTransition_UpdatesStatus()
    {
        // Arrange
        var order = new Order
        {
            UserId = "user-status-flow",
            Status = OrderStatus.Pending,
            TotalAmount = 120.00m
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Processing);

        // Assert
        Assert.True(success);
        var updated = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Processing, updated!.Status);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WhenOrderCancelled_PreventsModifyingStatus()
    {
        // Arrange
        var order = new Order
        {
            UserId = "user-cancelled-fixed",
            Status = OrderStatus.Cancelled,
            TotalAmount = 80.00m
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Completed);

        // Assert
        Assert.False(success);
        Assert.Contains("đã bị hủy", message);

        var updated = await _db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Cancelled, updated!.Status);
    }
}
