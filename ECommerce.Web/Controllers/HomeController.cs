using ECommerce.Web.Data;
using ECommerce.Web.Models;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;

namespace ECommerce.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HomeController> _logger;
    private readonly ICurrencyService _fx;
    private readonly IWebHostEnvironment _env;

    public HomeController(ApplicationDbContext db, ILogger<HomeController> logger, ICurrencyService fx, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _fx = fx;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var featuredProducts = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(6)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();

        return View(featuredProducts);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Lưu lựa chọn tiền tệ hiển thị (cookie 1 năm), quay lại trang cũ.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public IActionResult SetCurrency(string code, string? returnUrl = null)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (_fx.IsSupported(normalized))
        {
            Response.Cookies.Append(_fx.CookieName, normalized!, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(),
                MaxAge = TimeSpan.FromDays(365),
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("/Home/Error/{statusCode:int?}")]
    public IActionResult Error(int? statusCode = null)
    {
        var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature != null)
        {
            _logger.LogError(exceptionHandlerPathFeature.Error,
                "Đã bắt ngoại lệ qua UseExceptionHandler tại đường dẫn: '{Path}'. RequestId: {RequestId}, TraceIdentifier: {TraceId}",
                exceptionHandlerPathFeature.Path,
                Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                HttpContext.TraceIdentifier);
        }

        var statusCodeReExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var originalPath = exceptionHandlerPathFeature?.Path ?? statusCodeReExecuteFeature?.OriginalPath;

        var effectiveStatusCode = statusCode ?? (exceptionHandlerPathFeature != null ? 500 : HttpContext.Response.StatusCode);
        if (effectiveStatusCode == 200)
        {
            effectiveStatusCode = 500;
        }

        if (effectiveStatusCode == 404)
        {
            _logger.LogWarning("Yêu cầu 404 Not Found tới đường dẫn: '{OriginalPath}'. RequestId: {RequestId}",
                originalPath ?? HttpContext.Request.Path.Value,
                Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }
        else if (effectiveStatusCode >= 500)
        {
            _logger.LogError("Lỗi máy chủ {StatusCode} xảy ra tại: '{OriginalPath}'. RequestId: {RequestId}",
                effectiveStatusCode,
                originalPath ?? HttpContext.Request.Path.Value,
                Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        }

        var model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = effectiveStatusCode,
            OriginalPath = originalPath
        };

        Response.StatusCode = effectiveStatusCode;
        return View("Error", model);
    }
}
