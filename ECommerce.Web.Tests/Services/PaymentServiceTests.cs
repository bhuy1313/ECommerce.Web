using ECommerce.Web.Data;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Services.Payments;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Services;

public class PaymentServiceTests : IDisposable
{
    private const decimal OrderTotal = 100000m;
    private const string TxnRef = "5-1700000000000";
    private static readonly DateTime SeedTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ApplicationDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly VnPayOptions _options;
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateInMemoryDbContext();
        _options = new VnPayOptions
        {
            TmnCode = "T",
            HashSecret = "secret",
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "https://app/payment/return"
        };
        _service = new PaymentService(
            _db,
            new IPaymentGateway[] { new VnPayGateway(_options), new CodPaymentGateway() },
            new Mock<ILogger<PaymentService>>().Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task HandleCallback_WhenValid_SetsOrderPaidAndTransactionSucceeded()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var transaction = await SeedTransactionAsync(order.Id);
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

        // Act
        var (success, message, orderId) = await _service.HandleCallbackAsync("VnPay", query);

        // Assert
        Assert.True(success);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Equal(order.Id, orderId);

        var updatedTransaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
        Assert.Equal(PaymentTransactionStatus.Succeeded, updatedTransaction.Status);

        var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PaymentStatus.Paid, updatedOrder.PaymentStatus);
        Assert.Equal(OrderStatus.Processing, updatedOrder.Status);
    }

    [Fact]
    public async Task HandleCallback_WhenCalledTwice_ProcessesOnlyOnce()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var transaction = await SeedTransactionAsync(order.Id);
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

        // Act
        var first = await _service.HandleCallbackAsync("VnPay", query);
        var afterFirst = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
        var second = await _service.HandleCallbackAsync("VnPay", query);
        var afterSecond = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);

        // Assert
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(afterFirst.UpdatedAt > SeedTimestamp, "lần gọi đầu phải ghi UpdatedAt");
        Assert.Equal(afterFirst.UpdatedAt, afterSecond.UpdatedAt);
        Assert.Equal(1, await _db.PaymentTransactions.CountAsync());
        Assert.Equal(1, await _db.PaymentTransactions.CountAsync(t => t.Status == PaymentTransactionStatus.Succeeded));

        var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PaymentStatus.Paid, updatedOrder.PaymentStatus);
    }

    [Fact]
    public async Task HandleCallback_WhenInvalidSignature_RejectsAndLeavesOrderPending()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var transaction = await SeedTransactionAsync(order.Id);
        var query = VnPaySignatureHelper.BuildCallbackQuery(
            _options,
            TxnRef,
            OrderTotal,
            secureHash: VnPaySignatureHelper.WrongSignature);

        // Act
        var (success, message, _) = await _service.HandleCallbackAsync("VnPay", query);

        // Assert
        Assert.False(success);
        Assert.Contains("Chữ ký", message);

        var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PaymentStatus.Pending, updatedOrder.PaymentStatus);
        Assert.Equal(OrderStatus.Pending, updatedOrder.Status);

        var updatedTransaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
        Assert.Equal(PaymentTransactionStatus.Pending, updatedTransaction.Status);
    }

    [Fact]
    public async Task HandleCallback_WhenAmountMismatch_Rejects()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var transaction = await SeedTransactionAsync(order.Id);
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal + 50000m);

        // Act
        var (success, message, orderId) = await _service.HandleCallbackAsync("VnPay", query);

        // Assert
        Assert.False(success);
        Assert.Contains("không khớp", message);
        Assert.Equal(order.Id, orderId);

        var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PaymentStatus.Pending, updatedOrder.PaymentStatus);

        var updatedTransaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
        Assert.Equal(PaymentTransactionStatus.Pending, updatedTransaction.Status);
    }

    [Fact]
    public async Task HandleCallback_WhenTransactionUnknown_Rejects()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, "999-1700000000000", OrderTotal);

        // Act
        var (success, message, orderId) = await _service.HandleCallbackAsync("VnPay", query);

        // Assert
        Assert.False(success);
        Assert.Contains("Không tìm thấy giao dịch", message);
        Assert.Null(orderId);
        Assert.Equal(0, await _db.PaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task HandleCallback_WhenProviderUnknown_Rejects()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

        // Act
        var (success, message, orderId) = await _service.HandleCallbackAsync("Momo", query);

        // Assert
        Assert.False(success);
        Assert.Contains("không được hỗ trợ", message);
        Assert.Null(orderId);
    }

    [Fact]
    public async Task HandleCallback_WhenGatewayHasNoCallback_Rejects()
    {
        // Arrange
        var order = await SeedOrderAsync();
        var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

        // Act
        var (success, message, orderId) = await _service.HandleCallbackAsync("Cod", query);

        // Assert
        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Null(orderId);
    }

    [Fact]
    public async Task StartPaymentAsync_ForVnPay_CreatesPendingTransactionAndReturnsRedirectUrl()
    {
        // Arrange
        var order = await SeedOrderAsync();

        // Act
        var result = await _service.StartPaymentAsync(order.Id, PaymentMethod.VnPay, "127.0.0.1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.RedirectUrl);
        Assert.Null(result.Error);

        var transaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(order.Id, transaction.OrderId);
        Assert.Equal("VnPay", transaction.Provider);
        Assert.Equal(result.TransactionRef, transaction.TransactionRef);
        Assert.Equal(OrderTotal, transaction.Amount);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
    }

    [Fact]
    public async Task StartPaymentAsync_ForCod_ConfirmsWithoutRedirect()
    {
        // Arrange
        var order = await SeedOrderAsync();

        // Act
        var result = await _service.StartPaymentAsync(order.Id, PaymentMethod.Cod, "127.0.0.1");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.Equal($"COD-{order.Id}", result.TransactionRef);

        var transaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync();
        Assert.Equal("Cod", transaction.Provider);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
    }

    [Fact]
    public async Task StartPaymentAsync_WhenOrderAlreadyPaid_Rejects()
    {
        // Arrange
        var order = await SeedOrderAsync(PaymentStatus.Paid, OrderStatus.Processing);

        // Act
        var result = await _service.StartPaymentAsync(order.Id, PaymentMethod.VnPay, "127.0.0.1");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(0, await _db.PaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task StartPaymentAsync_WhenOrderMissing_Rejects()
    {
        // Act
        var result = await _service.StartPaymentAsync(999, PaymentMethod.VnPay, "127.0.0.1");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.RedirectUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private async Task<Order> SeedOrderAsync(
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        OrderStatus status = OrderStatus.Pending)
    {
        var order = new Order
        {
            UserId = "user-payment",
            Status = status,
            PaymentStatus = paymentStatus,
            Method = PaymentMethod.VnPay,
            TotalAmount = OrderTotal
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return order;
    }

    private async Task<PaymentTransaction> SeedTransactionAsync(
        int orderId,
        PaymentTransactionStatus status = PaymentTransactionStatus.Pending)
    {
        var transaction = new PaymentTransaction
        {
            OrderId = orderId,
            Provider = "VnPay",
            TransactionRef = TxnRef,
            Amount = OrderTotal,
            Status = status,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        };
        _db.PaymentTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        return transaction;
    }
}
