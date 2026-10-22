using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

/// <summary>
/// Hồi quy cho màn hình sửa phương thức vận chuyển: mapping GET, cập nhật POST,
/// chặn ModelState invalid và xử lý id không tồn tại.
/// </summary>
public class AdminShippingMethodTests
{
    [Fact]
    public async Task EditShippingMethod_Get_ReturnsCurrentValues()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = connection;
        var method = NewMethod();
        db.ShippingMethods.Add(method);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = Assert.IsType<ViewResult>(await controller.EditShippingMethod(method.Id));

        Assert.Equal("ShippingMethodForm", result.ViewName);
        var model = Assert.IsType<ShippingMethodFormViewModel>(result.Model);
        Assert.Equal(method.Id, model.Id);
        Assert.Equal("Giao hàng nhanh", model.Name);
        Assert.Equal("Trong 2 ngày", model.Description);
        Assert.Equal(60_000m, model.Fee);
        Assert.Equal(2, model.DisplayOrder);
        Assert.False(model.IsActive);
    }

    [Fact]
    public async Task EditShippingMethod_Post_UpdatesAndRedirects()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = connection;
        var method = NewMethod();
        db.ShippingMethods.Add(method);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = Assert.IsType<RedirectToActionResult>(await controller.EditShippingMethod(new ShippingMethodFormViewModel
        {
            Id = method.Id,
            Name = "  Hỏa tốc  ",
            Description = "   ",
            Fee = 45_000m,
            DisplayOrder = 3,
            IsActive = true
        }));

        Assert.Equal(nameof(AdminController.ShippingMethods), result.ActionName);

        db.ChangeTracker.Clear();
        var updated = await db.ShippingMethods.AsNoTracking().SingleAsync(m => m.Id == method.Id);
        Assert.Equal("Hỏa tốc", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(45_000m, updated.Fee);
        Assert.Equal(3, updated.DisplayOrder);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task EditShippingMethod_Post_WhenModelStateInvalid_DoesNotChangeEntity()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = connection;
        var method = NewMethod();
        db.ShippingMethods.Add(method);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        controller.ModelState.AddModelError(nameof(ShippingMethodFormViewModel.Name), "required");

        var result = Assert.IsType<ViewResult>(await controller.EditShippingMethod(new ShippingMethodFormViewModel
        {
            Id = method.Id,
            Name = "Tên mới",
            Fee = 99_000m
        }));

        Assert.Equal("ShippingMethodForm", result.ViewName);
        Assert.IsType<ShippingMethodFormViewModel>(result.Model);

        db.ChangeTracker.Clear();
        var unchanged = await db.ShippingMethods.AsNoTracking().SingleAsync(m => m.Id == method.Id);
        Assert.Equal("Giao hàng nhanh", unchanged.Name);
        Assert.Equal(60_000m, unchanged.Fee);
    }

    [Fact]
    public async Task EditShippingMethod_WhenMissing_RedirectsToIndex()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = connection;
        var controller = CreateController(db);

        var result = Assert.IsType<RedirectToActionResult>(await controller.EditShippingMethod(999_999));

        Assert.Equal(nameof(AdminController.ShippingMethods), result.ActionName);
    }

    private static ShippingMethod NewMethod() => new()
    {
        Name = "Giao hàng nhanh",
        Description = "Trong 2 ngày",
        Fee = 60_000m,
        DisplayOrder = 2,
        IsActive = false
    };

    private static AdminController CreateController(ApplicationDbContext db)
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var users = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        var roles = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object, null!, null!, null!, null!);

        return new AdminController(
            db,
            Mock.Of<IOrderService>(),
            Mock.Of<ICouponService>(),
            Mock.Of<IReviewService>(),
            Mock.Of<IShippingCalculator>(),
            users.Object,
            roles.Object,
            Mock.Of<ILogger<AdminController>>(),
            Mock.Of<INotyfService>());
    }
}
