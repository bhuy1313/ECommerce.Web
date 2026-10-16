using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
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

    public OrdersController(IOrderService orderService, UserManager<ApplicationUser> userManager, INotyfService notyf)
    {
        _orderService = orderService;
        _userManager = userManager;
        _notyf = notyf;
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
    public async Task<IActionResult> Checkout()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var (success, message, orderId) = await _orderService.CreateOrderFromCartAsync(userId);
        if (success && orderId.HasValue)
        {
            _notyf.Success(message);
            return RedirectToAction(nameof(Details), new { id = orderId.Value });
        }

        _notyf.Error(message);
        return RedirectToAction("Index", "Cart");
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
