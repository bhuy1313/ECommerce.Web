using System.Security.Claims;
using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Services;

public class CartServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<CartService>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;

    public CartServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateInMemoryDbContext();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContext = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(_httpContext);

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _loggerMock = new Mock<ILogger<CartService>>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private CartService CreateServiceForUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, $"{userId}@test.com") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        _userManagerMock.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        return new CartService(_db, _httpContextAccessorMock.Object, _userManagerMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddItemAsync_WhenQuantityNegativeOrZero_ReturnsFailure(int invalidQuantity)
    {
        // Arrange
        var service = CreateServiceForUser("user-invalid-qty");

        // Act
        var (success, message) = await service.AddItemAsync(1, invalidQuantity);

        // Assert
        Assert.False(success);
        Assert.Contains("lớn hơn 0", message);
    }

    [Fact]
    public async Task AddItemAsync_WhenProductOutOfStock_ReturnsFailure()
    {
        // Arrange
        var service = CreateServiceForUser("user-out-of-stock");
        var category = new Category { Name = "General", IsActive = true };
        var product = new Product { Name = "Sold Out Product", Price = 99m, StockQuantity = 0, IsActive = true, Category = category };
        _db.Categories.Add(category);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await service.AddItemAsync(product.Id, 1);

        // Assert
        Assert.False(success);
        Assert.Contains("tạm hết hàng", message);
    }

    [Fact]
    public async Task AddItemAsync_WhenQuantityExceedsStock_CapsAtAvailableStock()
    {
        // Arrange
        var userId = "user-cap-stock";
        var service = CreateServiceForUser(userId);
        var category = new Category { Name = "General", IsActive = true };
        var product = new Product { Name = "Limited Stock Item", Price = 50m, StockQuantity = 3, IsActive = true, Category = category };
        _db.Categories.Add(category);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // Act: Add 10 items when only 3 are in stock
        var (success, message) = await service.AddItemAsync(product.Id, 10);

        // Assert
        Assert.True(success);
        Assert.Contains("tối đa 3 sản phẩm", message);

        var cart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(cart);
        Assert.Single(cart!.Items);
        Assert.Equal(3, cart.Items.First().Quantity); // Capped to 3
    }

    [Fact]
    public async Task UpdateQuantityAsync_WhenZero_RemovesItemFromCart()
    {
        // Arrange
        var userId = "user-zero-update";
        var service = CreateServiceForUser(userId);
        var product = new Product { Name = "Removable Item", Price = 20m, StockQuantity = 10, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cart = new Cart { UserId = userId };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 2, UnitPrice = 20m });
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        // Act
        var (success, message) = await service.UpdateQuantityAsync(product.Id, 0);

        // Assert
        Assert.True(success);
        Assert.Contains("Đã xóa", message);

        var updatedCart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.Empty(updatedCart!.Items);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesItemFromCart()
    {
        // Arrange
        var userId = "user-direct-remove";
        var service = CreateServiceForUser(userId);
        var product = new Product { Name = "Item To Remove", Price = 15m, StockQuantity = 5, IsActive = true };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cart = new Cart { UserId = userId };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1, UnitPrice = 15m });
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();

        // Act
        await service.RemoveItemAsync(product.Id);

        // Assert
        var updatedCart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.Empty(updatedCart!.Items);
    }
}
