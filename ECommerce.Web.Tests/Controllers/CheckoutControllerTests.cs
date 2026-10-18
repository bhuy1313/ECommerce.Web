using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

/// <summary>
/// Checkout phải là thao tác có ràng buộc: khi thông tin nhận hàng không hợp lệ,
/// controller render lại form và KHÔNG tạo đơn (không trừ kho, không phát sinh Order).
/// </summary>
public class CheckoutControllerTests
{
    private static OrdersController CreateController(
        Mock<IOrderService> orderService,
        Mock<ICartService> cartService,
        string userId = "user-checkout")
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        var notyf = new Mock<INotyfService>();

        var controller = new OrdersController(orderService.Object, userManager.Object, notyf.Object, cartService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task Checkout_WhenShippingInfoInvalid_ReRendersFormAndDoesNotCreateOrder()
    {
        // Arrange
        var orderService = new Mock<IOrderService>();
        var cartService = new Mock<ICartService>();
        cartService.Setup(s => s.GetCartAsync()).ReturnsAsync(new CartViewModel());

        var controller = CreateController(orderService, cartService);
        var model = new CheckoutViewModel { Cart = new CartViewModel() };
        controller.ModelState.AddModelError(nameof(CheckoutViewModel.ShippingAddress), "Vui lòng nhập địa chỉ giao hàng.");

        // Act
        var result = await controller.Checkout(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Cart/CheckoutPreview.cshtml", view.ViewName);
        Assert.True(
            File.Exists(ResolveAppRelativePath(view.ViewName!)),
            $"View '{view.ViewName}' does not resolve to an existing file (resolved: " +
            $"'{ResolveAppRelativePath(view.ViewName!)}'). A bare view name would be " +
            "searched under /Views/Orders/ and /Views/Shared/ and throw at runtime, because the " +
            "checkout view lives in /Views/Cart/.");
        orderService.Verify(
            s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>()),
            Times.Never);
    }

    private static string ResolveAppRelativePath(string viewName)
    {
        Assert.StartsWith("~/", viewName);

        var relative = viewName[2..].Replace('/', Path.DirectorySeparatorChar);

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var projectDir = Path.Combine(dir.FullName, "ECommerce.Web");
            if (Directory.Exists(Path.Combine(projectDir, "Views")))
            {
                return Path.Combine(projectDir, relative);
            }
        }

        throw new InvalidOperationException(
            $"Could not locate the ECommerce.Web project folder walking up from '{AppContext.BaseDirectory}'.");
    }
}
