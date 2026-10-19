using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Email;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
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
        Mock<IPaymentService>? paymentService = null,
        string userId = "user-checkout",
        Mock<IEmailService>? emailService = null,
        Mock<ICouponService>? couponService = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);

        var notyf = new Mock<INotyfService>();

        couponService ??= new Mock<ICouponService>();
        couponService
            .Setup(c => c.ValidateAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
            .ReturnsAsync(new CouponValidationResult(false, null, null, 0m, null));

        var controller = new OrdersController(
            orderService.Object,
            userManager.Object,
            notyf.Object,
            cartService.Object,
            couponService.Object,
            (paymentService ?? new Mock<IPaymentService>()).Object,
            (emailService ?? new Mock<IEmailService>()).Object,
            Mock.Of<ILogger<OrdersController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://shop.test/Orders/Details/1");
        controller.Url = url.Object;

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
            s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Checkout_WhenPaymentMethodUndefined_ReRendersFormAndDoesNotCreateOrder()
    {
        // Arrange
        var orderService = new Mock<IOrderService>();
        var cartService = new Mock<ICartService>();
        cartService.Setup(s => s.GetCartAsync()).ReturnsAsync(new CartViewModel());

        var controller = CreateController(orderService, cartService);
        var model = new CheckoutViewModel { Cart = new CartViewModel() };

        // Model binding từ chối giá trị enum ngoài miền; test này ghim rằng hệ quả là
        // render lại form chứ KHÔNG tạo đơn.
        controller.ModelState.AddModelError(
            nameof(CheckoutViewModel.PaymentMethod),
            "Vui lòng chọn phương thức thanh toán hợp lệ.");

        // Act
        var result = await controller.Checkout(model);

        // Assert
        Assert.Equal("~/Views/Cart/CheckoutPreview.cshtml", Assert.IsType<ViewResult>(result).ViewName);
        Assert.True(controller.ModelState.ContainsKey(nameof(CheckoutViewModel.PaymentMethod)));
        orderService.Verify(
            s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Checkout_WhenValid_ForwardsSelectedPaymentMethodToOrderService()
    {
        // Pin hợp đồng: phương thức khách chọn phải được chuyển nguyên vẹn xuống
        // OrderService — không bị rơi về mặc định Cod trên đường đi.
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((true, "Đặt hàng thành công!", 77));

        var paymentService = new Mock<IPaymentService>();
        paymentService
            .Setup(s => s.StartPaymentAsync(It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentInitResult { Success = true });

        var cartService = new Mock<ICartService>();
        var controller = CreateController(orderService, cartService, paymentService);

        var model = new CheckoutViewModel
        {
            RecipientName = "Nguyen Van A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi, Q1",
            PaymentMethod = PaymentMethod.VnPay,
            Cart = new CartViewModel()
        };

        // Act
        var result = await controller.Checkout(model);

        // Assert
        orderService.Verify(
            s => s.CreateOrderFromCartAsync("user-checkout", It.IsAny<ShippingDetails>(), PaymentMethod.VnPay, It.IsAny<string?>()),
            Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(77, redirect.RouteValues?["id"]);
    }

    [Fact]
    public async Task Checkout_WhenStartPaymentThrows_StillRedirectsToOrderDetails()
    {
        // Đơn đã tạo (tồn kho đã trừ). Lỗi ở bước khởi tạo thanh toán KHÔNG được biến
        // thành 500 — đơn vẫn phải nằm trong lịch sử ở trạng thái chưa thanh toán.
        // Arrange
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((true, "Đặt hàng thành công!", 88));

        var paymentService = new Mock<IPaymentService>();
        paymentService
            .Setup(s => s.StartPaymentAsync(It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var controller = CreateController(orderService, new Mock<ICartService>(), paymentService);
        var model = new CheckoutViewModel
        {
            RecipientName = "A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            PaymentMethod = PaymentMethod.Cod,
            Cart = new CartViewModel()
        };

        // Act
        var result = await controller.Checkout(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(88, redirect.RouteValues?["id"]);
    }

    [Fact]
    public async Task Checkout_WhenOrderCreated_SendsOrderConfirmationEmailOnce()
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(s => s.CreateOrderFromCartAsync(
                It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((true, "ok", 77));
        var paymentService = new Mock<IPaymentService>();
        paymentService.Setup(s => s.StartPaymentAsync(
                It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentInitResult { Success = true });
        var email = new Mock<IEmailService>();

        var controller = CreateController(orderService, new Mock<ICartService>(), paymentService, emailService: email);
        var model = new CheckoutViewModel
        {
            RecipientName = "A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            PaymentMethod = PaymentMethod.VnPay,
            Cart = new CartViewModel()
        };

        await controller.Checkout(model);

        email.Verify(e => e.SendOrderConfirmationAsync(77, "https://shop.test/Orders/Details/1"), Times.Once);
    }

    [Fact]
    public async Task Checkout_WhenOrderEmailThrows_StillRedirectsToOrderDetails()
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(s => s.CreateOrderFromCartAsync(
                It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((true, "ok", 88));
        var paymentService = new Mock<IPaymentService>();
        paymentService.Setup(s => s.StartPaymentAsync(
                It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentInitResult { Success = true });
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendOrderConfirmationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var controller = CreateController(orderService, new Mock<ICartService>(), paymentService, emailService: email);
        var model = new CheckoutViewModel
        {
            RecipientName = "A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            PaymentMethod = PaymentMethod.Cod,
            Cart = new CartViewModel()
        };

        var result = await controller.Checkout(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
        Assert.Equal(88, redirect.RouteValues?["id"]);
    }

    [Fact]
    public async Task Checkout_PassesCouponCodeToOrderService()
    {
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.CreateOrderFromCartAsync(
                It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((true, "ok", 77));

        var controller = CreateController(orderService, new Mock<ICartService>());
        var model = new CheckoutViewModel
        {
            RecipientName = "A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            PaymentMethod = PaymentMethod.Cod,
            CouponCode = "SAVE10",
            Cart = new CartViewModel()
        };

        await controller.Checkout(model);

        orderService.Verify(
            s => s.CreateOrderFromCartAsync(
                "user-checkout", It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), "SAVE10"),
            Times.Once);
    }

    [Fact]
    public async Task Checkout_WhenOrderServiceFails_RedirectsToCheckoutWithCouponCode()
    {
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.CreateOrderFromCartAsync(
                It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), It.IsAny<string?>()))
            .ReturnsAsync((false, "Mã giảm giá đã hết hạn.", (int?)null));

        var controller = CreateController(orderService, new Mock<ICartService>());
        var model = new CheckoutViewModel
        {
            RecipientName = "A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            PaymentMethod = PaymentMethod.Cod,
            CouponCode = "SAVE10",
            Cart = new CartViewModel()
        };

        var result = await controller.Checkout(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Checkout", redirect.ActionName);
        Assert.Equal("Cart", redirect.ControllerName);
        Assert.Equal("SAVE10", redirect.RouteValues?["couponCode"]);
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
