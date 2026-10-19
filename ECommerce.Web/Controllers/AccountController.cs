using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.Constants;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICartService _cartService;
    private readonly IEmailService _emailService;
    private readonly INotyfService _notyf;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICartService cartService,
        IEmailService emailService,
        INotyfService notyf,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _cartService = cartService;
        _emailService = emailService;
        _notyf = notyf;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            // Chống user-enumeration: không tiết lộ email đã tồn tại (chỉ log server).
            _logger.LogWarning("Đăng ký với email đã tồn tại: {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Không thể tạo tài khoản với email này. Nếu bạn đã có tài khoản, vui lòng đăng nhập.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("Người dùng tạo tài khoản mới thành công: {Email}", model.Email);
            await _userManager.AddToRoleAsync(user, AppRoles.Customer);
            await TrySendConfirmationEmailAsync(user);
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _cartService.MergeGuestCartAsync(user.Id);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            // Chống user-enumeration: lockout trả cùng message chung như sai mật khẩu.
            _logger.LogWarning("Tài khoản {Email} bị tạm khóa do đăng nhập sai nhiều lần.", model.Email);
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (result.Succeeded)
        {
            _logger.LogInformation("Người dùng đăng nhập thành công: {Email}", model.Email);

            var loggedInUser = await _userManager.FindByEmailAsync(model.Email);
            if (loggedInUser != null)
            {
                await _cartService.MergeGuestCartAsync(loggedInUser.Id);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Người dùng đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || !IdentityTokenCodec.TryDecode(code, out var token))
        {
            return View(InvalidConfirmation());
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("Xác thực email cho user không tồn tại: {UserId}", userId);
            return View(InvalidConfirmation());
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Xác thực email thất bại cho {Email}: {Errors}", user.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return View(InvalidConfirmation());
        }

        _logger.LogInformation("Đã xác thực email cho {Email}", user.Email);
        return View(new ConfirmEmailViewModel(true, "Email của bạn đã được xác thực thành công."));
    }

    [HttpGet]
    public IActionResult ResendEmailConfirmation()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendEmailConfirmation(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            await TrySendConfirmationEmailAsync(user);
        }
        else
        {
            _logger.LogInformation("Bỏ qua gửi lại email xác thực cho {Email}.", model.Email);
        }

        _notyf.Information("Nếu email tồn tại và chưa xác thực, chúng tôi đã gửi lại liên kết xác thực.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task TrySendConfirmationEmailAsync(ApplicationUser user)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = IdentityTokenCodec.Encode(token);
            var link = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, code },
                Request.Scheme);

            if (string.IsNullOrEmpty(link))
            {
                _logger.LogWarning("Không tạo được liên kết xác thực email cho {Email}.", user.Email);
                return;
            }

            await _emailService.SendEmailConfirmationAsync(user.Email, link);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi email xác thực thất bại cho {Email}.", user.Email);
        }
    }

    private static ConfirmEmailViewModel InvalidConfirmation() =>
        new(false, "Liên kết xác thực không hợp lệ hoặc đã hết hạn.");
}
