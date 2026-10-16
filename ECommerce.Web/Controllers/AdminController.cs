using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Data;
using ECommerce.Web.Models.Constants;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Identity;

namespace ECommerce.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrOperator)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IOrderService _orderService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AdminController> _logger;
    private readonly INotyfService _notyf;

    public AdminController(
        ApplicationDbContext db,
        IOrderService orderService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<AdminController> logger,
        INotyfService notyf)
    {
        _db = db;
        _orderService = orderService;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _notyf = notyf;
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Index()
    {
        var totalProducts = await _db.Products.CountAsync();
        var activeProducts = await _db.Products.CountAsync(p => p.IsActive);
        var totalCategories = await _db.Categories.CountAsync();
        var lowStockProducts = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.StockQuantity < AppConstants.LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .AsNoTracking()
            .ToListAsync();

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();

        var totalRevenue = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Sum(o => o.TotalAmount);

        var totalUsers = await _userManager.Users.CountAsync();

        var model = new AdminDashboardViewModel
        {
            TotalRevenue = totalRevenue,
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
            ProcessingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
            ShippedOrders = orders.Count(o => o.Status == OrderStatus.Shipped),
            CompletedOrders = orders.Count(o => o.Status == OrderStatus.Completed),
            CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            TotalCategories = totalCategories,
            LowStockCount = lowStockProducts.Count,
            TotalUsers = totalUsers,
            RecentOrders = orders.Take(6).ToList(),
            LowStockProducts = lowStockProducts.Take(10).ToList()
        };

        return View(model);
    }

    // ==========================================
    // QUẢN LÝ DANH MỤC (CATEGORY CRUD)
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var categories = await _db.Categories
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();

        return View(categories);
    }

    [HttpGet]
    public IActionResult CreateCategory()
    {
        return View("CategoryForm", new CategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CategoryForm", model);
        }

        var isDuplicate = await _db.Categories.AnyAsync(c => c.Name.ToLower() == model.Name.Trim().ToLower());
        if (isDuplicate)
        {
            ModelState.AddModelError(nameof(model.Name), "Tên danh mục này đã tồn tại.");
            return View("CategoryForm", model);
        }

        var category = new Category
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        _notyf.Success($"Đã tạo danh mục '{category.Name}' thành công.");
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var model = new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        };

        return View("CategoryForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CategoryForm", model);
        }

        var category = await _db.Categories.FindAsync(model.Id);
        if (category == null)
        {
            return NotFound();
        }

        var isDuplicate = await _db.Categories.AnyAsync(c => c.Id != model.Id && c.Name.ToLower() == model.Name.Trim().ToLower());
        if (isDuplicate)
        {
            ModelState.AddModelError(nameof(model.Name), "Tên danh mục này đã được sử dụng bởi danh mục khác.");
            return View("CategoryForm", model);
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description?.Trim();
        category.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        _notyf.Success($"Đã cập nhật danh mục '{category.Name}' thành công.");
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategoryStatus(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        category.IsActive = !category.IsActive;
        await _db.SaveChangesAsync();

        _notyf.Success($"Đã {(category.IsActive ? "kích hoạt" : "vô hiệu hóa")} danh mục '{category.Name}'.");
        return RedirectToAction(nameof(Categories));
    }

    // ==========================================
    // QUẢN LÝ SẢN PHẨM (PRODUCT CRUD & ARCHIVE)
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Products(int? categoryId, string? search)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .AsQueryable();

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
        var categories = await _db.Categories.OrderBy(c => c.Name).AsNoTracking().ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.SearchQuery = search;

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> CreateProduct()
    {
        var model = new ProductFormViewModel
        {
            CategoryOptions = await GetCategorySelectListAsync()
        };
        return View("ProductForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await GetCategorySelectListAsync();
            return View("ProductForm", model);
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == model.CategoryId);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục được chọn không tồn tại.");
            model.CategoryOptions = await GetCategorySelectListAsync();
            return View("ProductForm", model);
        }

        var product = new Product
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Price = model.Price,
            StockQuantity = model.StockQuantity,
            CategoryId = model.CategoryId,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        _notyf.Success($"Đã thêm sản phẩm '{product.Name}' thành công.");
        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            IsActive = product.IsActive,
            CategoryOptions = await GetCategorySelectListAsync()
        };

        return View("ProductForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(ProductFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await GetCategorySelectListAsync();
            return View("ProductForm", model);
        }

        var product = await _db.Products.FindAsync(model.Id);
        if (product == null)
        {
            return NotFound();
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == model.CategoryId);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục được chọn không tồn tại.");
            model.CategoryOptions = await GetCategorySelectListAsync();
            return View("ProductForm", model);
        }

        product.Name = model.Name.Trim();
        product.Description = model.Description?.Trim();
        product.Price = model.Price;
        product.StockQuantity = model.StockQuantity;
        product.CategoryId = model.CategoryId;
        product.IsActive = model.IsActive;

        await _db.SaveChangesAsync();

        _notyf.Success($"Đã cập nhật sản phẩm '{product.Name}' thành công.");
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductStatus(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        product.IsActive = !product.IsActive;
        await _db.SaveChangesAsync();

        _notyf.Success($"Đã {(product.IsActive ? "kích hoạt kinh doanh" : "lưu kho / ngưng kinh doanh")} sản phẩm '{product.Name}'.");
        return RedirectToAction(nameof(Products));
    }

    private async Task<IEnumerable<SelectListItem>> GetCategorySelectListAsync()
    {
        return await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    // ==========================================
    // QUẢN LÝ ĐƠN HÀNG (ORDERS MANAGEMENT)
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Orders(OrderStatus? status, string? search)
    {
        var orders = await _orderService.GetAllOrdersAsync(status, search);
        ViewBag.SelectedStatus = status;
        ViewBag.SearchQuery = search;

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetails(int id)
    {
        var order = await _orderService.GetOrderDetailsAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus status)
    {
        var (success, message) = await _orderService.UpdateOrderStatusAsync(orderId, status);
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }

        return RedirectToAction(nameof(OrderDetails), new { id = orderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdatePaymentStatus(int orderId, PaymentStatus status)
    {
        var (success, message) = await _orderService.UpdatePaymentStatusAsync(orderId, status);
        if (success)
        {
            _notyf.Success(message);
        }
        else
        {
            _notyf.Error(message);
        }

        return RedirectToAction(nameof(OrderDetails), new { id = orderId });
    }

    // ==========================================
    // QUẢN LÝ NGƯỜI DÙNG & PHÂN QUYỀN (USER MANAGEMENT)
    // ==========================================

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Users()
    {
        var currentUserId = _userManager.GetUserId(User);
        var users = await _userManager.Users
            .Include(u => u.Orders)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var userList = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userList.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Roles = roles,
                OrderCount = user.Orders.Count,
                IsCurrentAdmin = user.Id == currentUserId
            });
        }

        return View(userList);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> EditUserRoles(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roleOrder = new[] { AppRoles.Admin, AppRoles.StoreOperator, AppRoles.Customer };
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        var sortedRoles = roleOrder.Where(r => allRoles.Contains(r)).ToList();
        var userRoles = await _userManager.GetRolesAsync(user);

        var model = new EditUserRolesViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = sortedRoles.Select(r => new RoleSelectionViewModel
            {
                RoleName = r,
                IsSelected = userRoles.Contains(r)
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> EditUserRoles(EditUserRolesViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        var selectedRoles = model.Roles.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();

        // Safeguard 1: Admin không được tự tước quyền Admin của chính mình
        if (user.Id == currentUserId && !selectedRoles.Contains(AppRoles.Admin))
        {
            ModelState.AddModelError(string.Empty, "Bạn không thể tự gỡ quyền Quản trị viên (Admin) của chính tài khoản mình đang sử dụng.");
            return View(model);
        }

        // Safeguard 2: Phải luôn có ít nhất 1 Admin trong hệ thống
        var allAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var isUserAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        if (isUserAdmin && !selectedRoles.Contains(AppRoles.Admin) && allAdmins.Count <= 1)
        {
            ModelState.AddModelError(string.Empty, "Không thể gỡ quyền Admin vì đây là Quản trị viên duy nhất còn lại trong hệ thống.");
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Gỡ bỏ role bị uncheck
        var rolesToRemove = currentRoles.Except(selectedRoles);
        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

        // Thêm role mới check
        var rolesToAdd = selectedRoles.Except(currentRoles);
        await _userManager.AddToRolesAsync(user, rolesToAdd);

        _logger.LogInformation("Admin '{AdminUser}' đã cập nhật vai trò của tài khoản '{UserEmail}' thành [{Roles}]",
            User.Identity?.Name ?? "Admin", user.Email, string.Join(", ", selectedRoles));

        _notyf.Success($"Đã cập nhật vai trò phân quyền cho tài khoản '{user.Email}' thành công.");
        return RedirectToAction(nameof(Users));
    }
}

