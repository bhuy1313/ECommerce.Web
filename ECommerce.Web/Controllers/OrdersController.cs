using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotyfService _notyf;
    private readonly ICartService _cartService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        UserManager<ApplicationUser> userManager,
        INotyfService notyf,
        ICartService cartService,
        IPaymentService paymentService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _userManager = userManager;
        _notyf = notyf;
        _cartService = cartService;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var orders = await _orderService.GetCustomerOrdersAsync(userId);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var order = await _orderService.GetOrderDetailsAsync(id, userId);
        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            model.Cart = await _cartService.GetCartAsync();
            _notyf.Error("Vui lòng kiểm tra thông tin nhận hàng.");
            return View("~/Views/Cart/CheckoutPreview.cshtml", model);
        }

        var shipping = new ShippingDetails
        {
            RecipientName = model.RecipientName,
            RecipientPhone = model.RecipientPhone,
            ShippingAddress = model.ShippingAddress,
            Note = model.Note
        };

        var (success, message, orderId) = await _orderService.CreateOrderFromCartAsync(userId, shipping, model.PaymentMethod);
        if (!success || !orderId.HasValue)
        {
            _notyf.Error(message);
            return RedirectToAction("Index", "Cart");
        }

        // Đơn hàng đã được tạo (tồn kho đã trừ). Bước khởi tạo thanh toán chỉ ghi
        // PaymentTransaction và (với VNPay) trả URL cổng thanh toán; thất bại ở bước này
        // KHÔNG huỷ đơn — đơn vẫn nằm trong lịch sử ở trạng thái chưa thanh toán.
        // (Chưa có nút "thanh toán lại" trên trang chi tiết đơn: xem ghi chú trong report A10.)
        var payment = await _paymentService.StartPaymentAsync(
            orderId.Value,
            model.PaymentMethod,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

        if (payment.Success && !string.IsNullOrWhiteSpace(payment.RedirectUrl))
        {
            return Redirect(payment.RedirectUrl);
        }

        if (!payment.Success)
        {
            _logger.LogWarning("Không khởi tạo được thanh toán cho đơn {OrderId}: {Error}", orderId.Value, payment.Error);
        }

        _notyf.Success(message);
        return RedirectToAction(nameof(Details), new { id = orderId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var (success, message) = await _orderService.CancelOrderAsync(id, userId);
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
