using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviewService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotyfService _notyf;

    public ProductsController(
        ApplicationDbContext db,
        IReviewService reviewService,
        UserManager<ApplicationUser> userManager,
        INotyfService notyf)
    {
        _db = db;
        _reviewService = reviewService;
        _userManager = userManager;
        _notyf = notyf;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId, string? search)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category != null && p.Category.IsActive);

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(p => p.Name.Contains(trimmed) || (p.Description != null && p.Description.Contains(trimmed)));
        }

        var products = await query.OrderByDescending(p => p.CreatedAt).AsNoTracking().ToListAsync();
        var categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking().ToListAsync();
        var ratings = await _reviewService.GetSummariesAsync(products.Select(p => p.Id));

        var viewModel = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            SearchQuery = search,
            Ratings = ratings
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.Category != null && p.Category.IsActive);

        if (product == null)
        {
            return NotFound();
        }

        var summary = await _reviewService.GetSummaryAsync(id);
        var model = new ProductDetailsViewModel
        {
            Product = product,
            Reviews = await _reviewService.GetApprovedAsync(id),
            AverageRating = summary.AverageRating,
            ReviewCount = summary.Count
        };

        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(userId))
        {
            model.HasReviewed = await _reviewService.HasReviewedAsync(userId, id);
            model.CanReview = await _reviewService.CanReviewAsync(userId, id);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("mutating")]
    public async Task<IActionResult> AddReview(int id, ReviewFormViewModel model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            _notyf.Error(ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Dữ liệu đánh giá không hợp lệ.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var (success, message) = await _reviewService.CreateAsync(userId, id, model.Rating, model.Comment);
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
