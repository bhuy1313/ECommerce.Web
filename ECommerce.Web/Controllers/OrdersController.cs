using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Email;
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
    private readonly IEmailService _emailService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        UserManager<ApplicationUser> userManager,
        INotyfService notyf,
        ICartService cartService,
        IPaymentService paymentService,
        IEmailService emailService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _userManager = userManager;
        _notyf = notyf;
        _cartService = cartService;
        _paymentService = paymentService;
        _emailService = emailService;
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
        var orderUrl = Url.Action(nameof(Details), "Orders", new { id = orderId.Value }, Request.Scheme)
            ?? string.Empty;
        try
        {
            await _emailService.SendOrderConfirmationAsync(orderId.Value, orderUrl);
        }
        catch (Exception ex)
        {
            // Email xác nhận là phụ trợ: lỗi gửi mail không được làm hỏng đơn đã tạo.
            _logger.LogError(ex, "Không gửi được email xác nhận cho đơn hàng {OrderId}.", orderId.Value);
        }

        PaymentInitResult payment;
        try
        {
            payment = await _paymentService.StartPaymentAsync(
                orderId.Value,
                model.PaymentMethod,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

            if (!payment.Success)
            {
                _logger.LogWarning("Không khởi tạo được thanh toán cho đơn {OrderId}: {Error}", orderId.Value, payment.Error);
            }
        }
        catch (Exception ex)
        {
            // Lỗi bất ngờ ở bước này (ví dụ đụng unique index của PaymentTransaction) không được
            // biến thành 500: đơn đã tạo và tồn kho đã trừ, khách vẫn phải thấy đơn trong lịch sử.
            _logger.LogError(ex, "Không khởi tạo được thanh toán cho đơn {OrderId}.", orderId.Value);
            payment = new PaymentInitResult { Success = false, Error = ex.Message };
        }

        if (payment.Success && !string.IsNullOrWhiteSpace(payment.RedirectUrl))
        {
            return Redirect(payment.RedirectUrl);
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
