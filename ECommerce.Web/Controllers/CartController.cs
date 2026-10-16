using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly INotyfService _notyf;

    public CartController(ICartService cartService, INotyfService notyf)
    {
        _cartService = cartService;
        _notyf = notyf;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
    public async Task<IActionResult> RemoveItem(int productId)
    {
        await _cartService.RemoveItemAsync(productId);
        _notyf.Success("Đã xóa sản phẩm khỏi giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        await _cartService.ClearCartAsync();
        _notyf.Success("Đã làm trống giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
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

        // Đã thỏa mãn tất cả điều kiện stock và login (sẵn sàng chuyển giao cho Phase 5)
        return View("CheckoutPreview", cart);
    }
}
