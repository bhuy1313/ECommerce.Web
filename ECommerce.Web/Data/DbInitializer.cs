using ECommerce.Web.Models.Constants;
using ECommerce.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ECommerce.Web.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // 1. Seed Roles
        string[] roles = [AppRoles.Admin, AppRoles.StoreOperator, AppRoles.Customer];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Default Admin User
        var adminEmail = "admin@ecommerce.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createAdminResult = await userManager.CreateAsync(adminUser, "Admin@12345");
            if (createAdminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
            }
        }

        // 3. Seed Categories & Products (danh mục tiếng Việt, giá VNĐ)
        var canonicalCategories = new (string Name, string Description)[]
        {
            ("Thiết bị & Laptop", "Laptop, máy tính và thiết bị số"),
            ("Gaming & Phụ kiện", "Bàn phím, chuột và gear gaming"),
            ("Thời trang công sở", "Trang phục phong cách tối giản"),
            ("Sách chuyên ngành", "Sách lập trình và thiết kế"),
        };

        var categoryMap = new Dictionary<string, Category>();
        var canonicalNames = canonicalCategories.Select(c => c.Name).ToList();
        foreach (var (name, description) in canonicalCategories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
            if (existing == null)
            {
                existing = new Category { Name = name, Description = description };
                db.Categories.Add(existing);
            }
            categoryMap[name] = existing;
        }
        await db.SaveChangesAsync();

        // Dọn dữ liệu mẫu bẩn từ các lần seed cũ (tên chứa dãy số timestamp,
        // vd "Hi-Fi Headphones 179195..." / "Audio 179..."). Chỉ xóa sản phẩm
        // chưa phát sinh đơn hàng hay giỏ hàng để không phá vỡ FK.
        var timestamp = new Regex(@"\d{10,}", RegexOptions.Compiled);
        var allProducts = await db.Products
            .Include(p => p.Category)
            .Include(p => p.OrderItems)
            .Include(p => p.CartItems)
            .ToListAsync();

        var junkProducts = allProducts
            .Where(p => !p.OrderItems.Any() && !p.CartItems.Any()
                && (timestamp.IsMatch(p.Name) || (p.Category != null && timestamp.IsMatch(p.Category.Name))))
            .ToList();
        if (junkProducts.Count > 0)
        {
            db.Products.RemoveRange(junkProducts);
            await db.SaveChangesAsync();
        }

        var allCategories = await db.Categories.Include(c => c.Products).ToListAsync();
        var orphanedJunkCategories = allCategories
            .Where(c => timestamp.IsMatch(c.Name) && !c.Products.Any())
            .ToList();
        if (orphanedJunkCategories.Count > 0)
        {
            db.Categories.RemoveRange(orphanedJunkCategories);
            await db.SaveChangesAsync();
        }

        // Dọn sản phẩm mẫu cũ giá USD (vd 25.99, 52.00) còn sót từ bản seed tiếng Anh
        // và danh mục test (vd "VerifyCat_..."). Chỉ xóa khi chưa phát sinh đơn/giỏ.
        var legacyProducts = allProducts
            .Where(p => !p.OrderItems.Any() && !p.CartItems.Any()
                && (p.Price < 1000
                    || (p.Category != null && p.Category.Name.StartsWith("Verify"))))
            .ToList();
        if (legacyProducts.Count > 0)
        {
            db.Products.RemoveRange(legacyProducts);
            await db.SaveChangesAsync();
        }

        var remainingCategories = await db.Categories.Include(c => c.Products).ToListAsync();
        var emptyNonCanonical = remainingCategories
            .Where(c => !canonicalNames.Contains(c.Name) && !c.Products.Any())
            .ToList();
        if (emptyNonCanonical.Count > 0)
        {
            db.Categories.RemoveRange(emptyNonCanonical);
            await db.SaveChangesAsync();
        }

        // Quy đổi giá USD sang VNĐ cho sản phẩm cũ còn sót (giữ lại vì vướng
        // đơn hàng/giỏ hàng; OrderItem đã snapshot giá nên lịch sử không đổi).
        // Điều kiện Price < 1000 giúp chỉ quy đổi đúng một lần.
        var usdPriced = allProducts
            .Where(p => p.Price < 1000 && (p.OrderItems.Any() || p.CartItems.Any()))
            .ToList();
        foreach (var p in usdPriced)
        {
            p.Price = Math.Round(p.Price * 25000);
        }
        if (usdPriced.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        // Quy đổi đơn hàng thời USD sang VNĐ (một lần, nhờ guard < 1000;
        // đơn VNĐ thật không bao giờ dưới 1000₫ với catalog hiện tại).
        var usdOrders = await db.Orders.Where(o => o.TotalAmount < 1000).ToListAsync();
        foreach (var o in usdOrders)
        {
            o.TotalAmount = Math.Round(o.TotalAmount * 25000);
        }
        var usdItems = await db.OrderItems.Where(i => i.UnitPrice < 1000).ToListAsync();
        foreach (var i in usdItems)
        {
            i.UnitPrice = Math.Round(i.UnitPrice * 25000);
        }
        if (usdOrders.Count + usdItems.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        // Seed sản phẩm mẫu nếu danh mục chuẩn chưa có sản phẩm nào.
        var hasCatalog = await db.Products
            .Include(p => p.Category)
            .AnyAsync(p => p.Category != null && canonicalNames.Contains(p.Category.Name));
        if (!hasCatalog)
        {
            var now = DateTime.UtcNow;
            db.Products.AddRange(
                new Product { Name = "Laptop UltraBook 14\"", Description = "Laptop mỏng nhẹ, hiệu năng cao, pin 18 giờ.", Price = 24990000m, StockQuantity = 12, Category = categoryMap["Thiết bị & Laptop"], CreatedAt = now },
                new Product { Name = "Bàn phím cơ RGB", Description = "Switch cơ gõ êm, LED RGB tùy chỉnh.", Price = 1890000m, StockQuantity = 20, Category = categoryMap["Gaming & Phụ kiện"], CreatedAt = now.AddMinutes(-10) },
                new Product { Name = "Áo thun cotton basic", Description = "100% cotton thoáng mát, form regular.", Price = 299000m, StockQuantity = 100, Category = categoryMap["Thời trang công sở"], CreatedAt = now.AddMinutes(-20) },
                new Product { Name = "Clean Code (Tái bản)", Description = "Cẩm nang viết code sạch của Robert C. Martin.", Price = 385000m, StockQuantity = 40, Category = categoryMap["Sách chuyên ngành"], CreatedAt = now.AddMinutes(-30) },
                new Product { Name = "Chuột không dây Silent", Description = "Click êm, pin 12 tháng, kết nối 2.4GHz.", Price = 549000m, StockQuantity = 60, Category = categoryMap["Thiết bị & Laptop"], CreatedAt = now.AddMinutes(-40) },
                new Product { Name = "Chuột gaming 26K DPI", Description = "Cảm biến 26K DPI, 8 nút lập trình.", Price = 990000m, StockQuantity = 0, Category = categoryMap["Gaming & Phụ kiện"], CreatedAt = now.AddMinutes(-50) },
                new Product { Name = "Áo khoác denim", Description = "Denim dày dặn, phong cách tối giản.", Price = 1199000m, StockQuantity = 9, Category = categoryMap["Thời trang công sở"], CreatedAt = now.AddMinutes(-60) },
                new Product { Name = "Design Patterns", Description = "Các mẫu thiết kế hướng đối tượng kinh điển.", Price = 420000m, StockQuantity = 25, Category = categoryMap["Sách chuyên ngành"], CreatedAt = now.AddMinutes(-70) },
                new Product { Name = "Hub USB-C 7-trong-1", Description = "Mở rộng HDMI, USB-A, LAN và sạc PD.", Price = 890000m, StockQuantity = 35, Category = categoryMap["Thiết bị & Laptop"], CreatedAt = now.AddMinutes(-80) });

            await db.SaveChangesAsync();
        }
    }
}