using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAddressService _addressService;
    private readonly INotyfService _notyf;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IAddressService addressService,
        INotyfService notyf)
    {
        _userManager = userManager;
        _addressService = addressService;
        _notyf = notyf;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var addresses = await _addressService.GetAddressesAsync(UserId);
        return View(new ProfileViewModel { Addresses = addresses });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> Add(AddressFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var addresses = await _addressService.GetAddressesAsync(UserId);
            return View("Index", new ProfileViewModel { Addresses = addresses, NewAddress = model });
        }

        var (success, message) = await _addressService.AddAddressAsync(
            UserId, model.RecipientName, model.RecipientPhone, model.AddressLine, model.MakeDefault);

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
    public async Task<IActionResult> SetDefault(int id)
    {
        var (success, message) = await _addressService.SetDefaultAddressAsync(UserId, id);
        Report(success, message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, message) = await _addressService.DeleteAddressAsync(UserId, id);
        Report(success, message);
        return RedirectToAction(nameof(Index));
    }

    private string UserId => _userManager.GetUserId(User)!;

    private void Report(bool success, string message)
    {
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }
    }
}
