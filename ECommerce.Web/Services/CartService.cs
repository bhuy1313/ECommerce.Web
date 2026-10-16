using System.Text.Json;
using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services;

public class CartService : ICartService
{
    private const string GuestCartCookie = "Ecommerce_GuestCart";
    private const int MaxGuestQuantity = 100000;
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CartService> _logger;
    private readonly IDataProtector? _cartProtector;
    private readonly bool _cookieSecure;

    public CartService(
        ApplicationDbContext db,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        ILogger<CartService> logger,
        IDataProtectionProvider? dataProtection = null,
        IWebHostEnvironment? webEnvironment = null)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _logger = logger;
        _cartProtector = dataProtection?.CreateProtector("Ecommerce_GuestCart.v1");
        // Localhost dev http vẫn chạy được; production ép Secure.
        _cookieSecure = webEnvironment == null || !webEnvironment.IsDevelopment();
    }

    public async Task<CartViewModel> GetCartAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        var viewModel = new CartViewModel();

        if (isAuthenticated)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p!.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                foreach (var item in cart.Items)
                {
                    if (item.Product != null)
                    {
                        viewModel.Items.Add(new CartItemViewModel
                        {
                            ProductId = item.ProductId,
                            ProductName = item.Product.Name,
                            CategoryName = item.Product.Category?.Name,
                            UnitPrice = item.Product.Price,
                            Quantity = item.Quantity,
                            AvailableStock = item.Product.StockQuantity,
                            IsActive = item.Product.IsActive && (item.Product.Category?.IsActive ?? false)
                        });
                    }
                }
            }
        }
        else
        {
            var guestItems = GetGuestCartItems();
            if (guestItems.Count > 0)
            {
                var productIds = guestItems.Select(g => g.ProductId).Distinct().ToList();
                var products = await _db.Products
                    .Include(p => p.Category)
                    .Where(p => productIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToDictionaryAsync(p => p.Id);

                foreach (var guestItem in guestItems)
                {
                    if (products.TryGetValue(guestItem.ProductId, out var product))
                    {
                        viewModel.Items.Add(new CartItemViewModel
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            CategoryName = product.Category?.Name,
                            UnitPrice = product.Price,
                            Quantity = guestItem.Quantity,
                            AvailableStock = product.StockQuantity,
                            IsActive = product.IsActive && (product.Category?.IsActive ?? false)
                        });
                    }
                }
            }
        }

        return viewModel;
    }

    public async Task<(bool Success, string Message)> AddItemAsync(int productId, int quantity = 1)
    {
        if (quantity <= 0)
        {
            return (false, "Số lượng thêm vào giỏ phải lớn hơn 0.");
        }

        var product = await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null || !product.IsActive || (product.Category != null && !product.Category.IsActive))
        {
            return (false, "Sản phẩm này hiện không còn khả dụng.");
        }

        if (product.StockQuantity <= 0)
        {
            return (false, "Sản phẩm hiện đang tạm hết hàng.");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        if (isAuthenticated)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId! };
                _db.Carts.Add(cart);
                await _db.SaveChangesAsync();
            }

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            var currentQuantity = item?.Quantity ?? 0;
            var targetQuantity = currentQuantity + quantity;

            var capped = false;
            if (targetQuantity > product.StockQuantity)
            {
                targetQuantity = product.StockQuantity;
                capped = true;
            }

            if (item == null)
            {
                item = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = targetQuantity,
                    UnitPrice = product.Price
                };
                cart.Items.Add(item);
            }
            else
            {
                item.Quantity = targetQuantity;
                item.UnitPrice = product.Price;
            }

            await _db.SaveChangesAsync();

            if (capped)
            {
                return (true, $"Đã cập nhật giỏ hàng. Chỉ có thể thêm tối đa {product.StockQuantity} sản phẩm do giới hạn tồn kho.");
            }

            return (true, $"Đã thêm {quantity} '{product.Name}' vào giỏ hàng.");
        }
        else
        {
            var guestItems = GetGuestCartItems();
            var existing = guestItems.FirstOrDefault(i => i.ProductId == productId);
            var currentQuantity = existing?.Quantity ?? 0;
            var targetQuantity = currentQuantity + quantity;

            var capped = false;
            if (targetQuantity > product.StockQuantity)
            {
                targetQuantity = product.StockQuantity;
                capped = true;
            }

            if (existing == null)
            {
                guestItems.Add(new GuestCartItemDto(productId, targetQuantity));
            }
            else
            {
                existing.Quantity = targetQuantity;
            }

            SaveGuestCartItems(guestItems);

            _logger.LogInformation("Thêm vào giỏ hàng: Sản phẩm ID {ProductId} ('{ProductName}'), Số lượng: {Quantity}, Người dùng: {UserIdentity}",
                product.Id, product.Name, quantity, isAuthenticated ? $"User {userId}" : "Guest");

            if (capped)
            {
                return (true, $"Đã cập nhật giỏ hàng. Chỉ có thể thêm tối đa {product.StockQuantity} sản phẩm do giới hạn tồn kho.");
            }

            return (true, $"Đã thêm {quantity} '{product.Name}' vào giỏ hàng.");
        }
    }

    public async Task<(bool Success, string Message)> UpdateQuantityAsync(int productId, int quantity)
    {
        if (quantity <= 0)
        {
            await RemoveItemAsync(productId);
            return (true, "Đã xóa sản phẩm khỏi giỏ hàng.");
        }

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
        {
            await RemoveItemAsync(productId);
            return (false, "Sản phẩm không tồn tại.");
        }

        var capped = false;
        var targetQuantity = quantity;
        if (targetQuantity > product.StockQuantity)
        {
            targetQuantity = product.StockQuantity;
            capped = true;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        if (isAuthenticated)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    item.Quantity = targetQuantity;
                    item.UnitPrice = product.Price;
                    await _db.SaveChangesAsync();
                }
            }
        }
        else
        {
            var guestItems = GetGuestCartItems();
            var item = guestItems.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                item.Quantity = targetQuantity;
                SaveGuestCartItems(guestItems);
            }
        }

        _logger.LogInformation("Cập nhật giỏ hàng: Sản phẩm ID {ProductId}, Số lượng mới: {Quantity}, Người dùng: {UserIdentity}",
            productId, targetQuantity, isAuthenticated ? $"User {userId}" : "Guest");

        if (capped)
        {
            return (true, $"Số lượng được điều chỉnh về {product.StockQuantity} theo tồn kho khả dụng.");
        }

        return (true, "Đã cập nhật số lượng thành công.");
    }

    public async Task RemoveItemAsync(int productId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        if (isAuthenticated)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null)
            {
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    _db.CartItems.Remove(item);
                    await _db.SaveChangesAsync();
                }
            }
        }
        else
        {
            var guestItems = GetGuestCartItems();
            guestItems.RemoveAll(i => i.ProductId == productId);
            SaveGuestCartItems(guestItems);
        }

        _logger.LogInformation("Xóa khỏi giỏ hàng: Sản phẩm ID {ProductId}, Người dùng: {UserIdentity}",
            productId, isAuthenticated ? $"User {userId}" : "Guest");
    }

    public async Task ClearCartAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        if (isAuthenticated)
        {
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null && cart.Items.Count > 0)
            {
                _db.CartItems.RemoveRange(cart.Items);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            DeleteGuestCartCookie();
        }
    }

    public async Task MergeGuestCartAsync(string userId)
    {
        var guestItems = GetGuestCartItems();
        if (guestItems.Count == 0)
        {
            return;
        }

        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync();
        }

        var productIds = guestItems.Select(g => g.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id);

        foreach (var guestItem in guestItems)
        {
            if (products.TryGetValue(guestItem.ProductId, out var product))
            {
                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
                if (existingItem != null)
                {
                    var newQty = Math.Min(existingItem.Quantity + guestItem.Quantity, product.StockQuantity);
                    existingItem.Quantity = newQty;
                    existingItem.UnitPrice = product.Price;
                }
                else
                {
                    var newQty = Math.Min(guestItem.Quantity, product.StockQuantity);
                    if (newQty > 0)
                    {
                        cart.Items.Add(new CartItem
                        {
                            CartId = cart.Id,
                            ProductId = guestItem.ProductId,
                            Quantity = newQty,
                            UnitPrice = product.Price
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync();
        DeleteGuestCartCookie();

        _logger.LogInformation("Hợp nhất giỏ hàng: Đã chuyển {Count} sản phẩm từ Cookie Guest sang User {UserId}",
            guestItems.Count, userId);
    }

    public async Task<int> GetCartItemCountAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(userId);

        if (isAuthenticated)
        {
            return await _db.CartItems
                .Where(ci => ci.Cart != null && ci.Cart.UserId == userId)
                .SumAsync(ci => ci.Quantity);
        }
        else
        {
            return GetGuestCartItems().Sum(i => i.Quantity);
        }
    }

    private List<GuestCartItemDto> GetGuestCartItems()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var cookie = httpContext?.Request.Cookies[GuestCartCookie];
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return new List<GuestCartItemDto>();
        }

        try
        {
            // Cookie do client kiểm soát: ưu tiên unprotect (định dạng mới có ký);
            // rớt về JSON trần để tương thích cookie cũ, thất bại nữa thì giỏ rỗng.
            var json = cookie;
            if (_cartProtector != null)
            {
                try
                {
                    json = _cartProtector.Unprotect(cookie);
                }
                catch
                {
                    json = cookie;
                }
            }

            var items = JsonSerializer.Deserialize<List<GuestCartItemDto>>(json)
                ?? new List<GuestCartItemDto>();
            // Lọc giá trị bất thường (số âm/khổng lồ, id không hợp lệ) trước khi dùng.
            return items
                .Where(i => i.ProductId > 0 && i.Quantity > 0 && i.Quantity <= MaxGuestQuantity)
                .GroupBy(i => i.ProductId)
                .Select(g => new GuestCartItemDto(g.Key, Math.Min(g.Sum(i => i.Quantity), MaxGuestQuantity)))
                .ToList();
        }
        catch
        {
            return new List<GuestCartItemDto>();
        }
    }

    private void SaveGuestCartItems(List<GuestCartItemDto> items)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        if (items.Count == 0)
        {
            DeleteGuestCartCookie();
            return;
        }

        var json = JsonSerializer.Serialize(items);
        var value = _cartProtector?.Protect(json) ?? json;
        httpContext.Response.Cookies.Append(GuestCartCookie, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSecure,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private void DeleteGuestCartCookie()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(GuestCartCookie);
    }

    private class GuestCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        public GuestCartItemDto() { }

        public GuestCartItemDto(int productId, int quantity)
        {
            ProductId = productId;
            Quantity = quantity;
        }
    }
}
