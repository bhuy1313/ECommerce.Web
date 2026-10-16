using ECommerce.Web.Data;
using ECommerce.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Controllers;

public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db)
    {
        _db = db;
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

        var viewModel = new ProductListViewModel
        {
            Products = products,
            Categories = categories,
            SelectedCategoryId = categoryId,
            SearchQuery = search
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

        return View(product);
    }
}
