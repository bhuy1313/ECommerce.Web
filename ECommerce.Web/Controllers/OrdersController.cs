using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
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
    private readonly ICouponService _couponService;
    private readonly IShippingCalculator _shippingCalculator;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrdersController> _logger;
    private readonly VnPayOptions _vnPayOptions;

    public OrdersController(
        IOrderService orderService,
        UserManager<ApplicationUser> userManager,
        INotyfService notyf,
        ICartService cartService,
        ICouponService couponService,
        IShippingCalculator shippingCalculator,
        IPaymentService paymentService,
        IEmailService emailService,
        ILogger<OrdersController> logger,
        VnPayOptions? vnPayOptions = null)
    {
        _orderService = orderService;
        _userManager = userManager;
        _notyf = notyf;
        _cartService = cartService;
        _couponService = couponService;
        _shippingCalculator = shippingCalculator;
        _paymentService = paymentService;
        _emailService = emailService;
        _logger = logger;
        _vnPayOptions = vnPayOptions ?? new VnPayOptions();
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
            _notyf.Error("Vui lòng kiểm tra thông tin nhận hàng.");
            await PopulateCheckoutAsync(model);
            return View("~/Views/Cart/CheckoutPreview.cshtml", model);
        }

        var methods = await _shippingCalculator.GetActiveMethodsAsync();
        if (methods.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ShippingMethodId), "Hiện chưa có phương thức vận chuyển khả dụng.");
            _notyf.Error("Hiện chưa có phương thức vận chuyển khả dụng.");
            await PopulateCheckoutAsync(model);
            return View("~/Views/Cart/CheckoutPreview.cshtml", model);
        }

        if (!model.ShippingMethodId.HasValue || methods.All(m => m.Id != model.ShippingMethodId.Value))
        {
            ModelState.AddModelError(nameof(model.ShippingMethodId), "Vui lòng chọn phương thức vận chuyển hợp lệ.");
            _notyf.Error("Vui lòng chọn phương thức vận chuyển hợp lệ.");
            await PopulateCheckoutAsync(model);
            return View("~/Views/Cart/CheckoutPreview.cshtml", model);
        }

        if (model.PaymentMethod == PaymentMethod.VnPay && !_vnPayOptions.IsConfigured)
        {
            ModelState.AddModelError(nameof(model.PaymentMethod),
                "Thanh toán VNPay hiện chưa khả dụng. Vui lòng chọn thanh toán khi nhận hàng (COD).");
            _notyf.Error("VNPay hiện chưa khả dụng, vui lòng chọn COD.");
            await PopulateCheckoutAsync(model);
            return View("~/Views/Cart/CheckoutPreview.cshtml", model);
        }

        var shipping = new ShippingDetails
        {
            RecipientName = model.RecipientName,
            RecipientPhone = model.RecipientPhone,
            ShippingAddress = model.ShippingAddress,
            Note = model.Note
        };

        var (success, message, orderId) = await _orderService.CreateOrderFromCartAsync(
            userId, shipping, model.PaymentMethod, model.CouponCode, model.ShippingMethodId);
        if (!success || !orderId.HasValue)
        {
            _notyf.Error(message);
            if (!string.IsNullOrWhiteSpace(model.CouponCode))
            {
                return RedirectToAction("Checkout", "Cart", new { couponCode = model.CouponCode });
            }

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

    private async Task PopulateCheckoutAsync(CheckoutViewModel model)
    {
        model.Cart = await _cartService.GetCartAsync();
        var coupon = await _couponService.ValidateAsync(model.CouponCode, model.Cart.TotalAmount);
        model.DiscountAmount = coupon.DiscountAmount;
        model.CouponError = coupon.Error;
        model.CouponDescription = coupon.Description;

        var methods = await _shippingCalculator.GetActiveMethodsAsync();
        model.ShippingMethods = methods
            .Select(m => new ShippingMethodOption(m.Id, m.Name, m.Fee, m.Description))
            .ToList();

        var selectedId = methods.Any(m => m.Id == model.ShippingMethodId)
            ? model.ShippingMethodId
            : methods.FirstOrDefault()?.Id;
        var quote = selectedId.HasValue
            ? await _shippingCalculator.QuoteAsync(selectedId, model.Cart.TotalAmount - coupon.DiscountAmount)
            : null;

        if (quote is null)
        {
            model.ShippingMethodId = null;
            model.ShippingFee = 0m;
            model.TaxRate = _shippingCalculator.TaxRate;
            model.TaxAmount = 0m;
            model.ShippingError = methods.Count == 0
                ? "Hiện chưa có phương thức vận chuyển khả dụng."
                : null;
            return;
        }

        model.ShippingMethodId = quote.ShippingMethodId;
        model.ShippingFee = quote.ShippingFee;
        model.TaxRate = quote.TaxRate;
        model.TaxAmount = quote.TaxAmount;
        model.ShippingError = null;
    }
}
