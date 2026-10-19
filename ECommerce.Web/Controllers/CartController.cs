using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly INotyfService _notyf;
    private readonly IAddressService _addressService;
    private readonly ICouponService _couponService;

    public CartController(
        ICartService cartService,
        INotyfService notyf,
        IAddressService addressService,
        ICouponService couponService)
    {
        _cartService = cartService;
        _notyf = notyf;
        _addressService = addressService;
        _couponService = couponService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1, string? returnUrl = null)
    {
        var (success, message) = await _cartService.AddItemAsync(productId, quantity);
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
    {
        var (success, message) = await _cartService.UpdateQuantityAsync(productId, quantity);
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> RemoveItem(int productId)
    {
        await _cartService.RemoveItemAsync(productId);
        _notyf.Success("Đã xóa sản phẩm khỏi giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> Clear()
    {
        await _cartService.ClearCartAsync();
        _notyf.Success("Đã làm trống giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(string? couponCode = null)
    {
        var cart = await _cartService.GetCartAsync();
        if (!cart.Items.Any())
        {
            _notyf.Error("Giỏ hàng của bạn đang trống.");
            return RedirectToAction(nameof(Index));
        }

        if (cart.HasAnyStockIssue)
        {
            _notyf.Error("Một số sản phẩm trong giỏ đã vượt quá tồn kho khả dụng. Vui lòng điều chỉnh lại trước khi thanh toán.");
            return RedirectToAction(nameof(Index));
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            _notyf.Information("Vui lòng đăng nhập tài khoản để tiếp tục thanh toán đơn hàng.");
            return RedirectToAction("Login", "Account", new { returnUrl = "/Cart/Checkout" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var addresses = userId is null
            ? Array.Empty<Address>()
            : (await _addressService.GetAddressesAsync(userId)).ToArray();
        var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault);
        var coupon = await _couponService.ValidateAsync(couponCode, cart.TotalAmount);

        return View("CheckoutPreview", new CheckoutViewModel
        {
            Cart = cart,
            RecipientName = defaultAddress?.RecipientName ?? string.Empty,
            RecipientPhone = defaultAddress?.RecipientPhone ?? string.Empty,
            ShippingAddress = defaultAddress?.AddressLine ?? string.Empty,
            SavedAddressId = defaultAddress?.Id,
            SavedAddresses = addresses
                .Select(a => new SavedAddressOption(
                    a.Id, $"{a.RecipientName} — {a.AddressLine}", a.RecipientName, a.RecipientPhone, a.AddressLine))
                .ToList(),
            CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim(),
            DiscountAmount = coupon.DiscountAmount,
            CouponError = coupon.Error,
            CouponDescription = coupon.Description
        });
    }
}
