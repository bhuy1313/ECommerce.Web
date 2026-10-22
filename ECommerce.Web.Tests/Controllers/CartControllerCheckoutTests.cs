using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

public class CartControllerCheckoutTests
{
    private static (CartController Controller, Mock<IAddressService> Addresses) CreateController(
        Mock<ICouponService>? coupons = null,
        Mock<IShippingCalculator>? shipping = null)
    {
        var cart = new Mock<ICartService>();
        cart.Setup(c => c.GetCartAsync()).ReturnsAsync(new CartViewModel
        {
            Items =
            {
                new CartItemViewModel
                {
                    ProductId = 1,
                    ProductName = "iPhone",
                    UnitPrice = 10_000_000m,
                    Quantity = 1,
                    AvailableStock = 5
                }
            }
        });

        var addresses = new Mock<IAddressService>();
        if (coupons is null)
        {
            coupons = new Mock<ICouponService>();
            coupons
                .Setup(c => c.ValidateAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .ReturnsAsync(new CouponValidationResult(false, null, null, 0m, null));
        }

        if (shipping is null)
        {
            shipping = new Mock<IShippingCalculator>();
            shipping
                .Setup(s => s.GetActiveMethodsAsync())
                .ReturnsAsync(Array.Empty<ShippingMethod>());
        }

        var controller = new CartController(
            cart.Object, Mock.Of<INotyfService>(), addresses.Object, coupons.Object, shipping.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "u1"),
                        new Claim(ClaimTypes.Name, "buyer@test.local")
                    },
                    "Test"))
            },
            RouteData = new RouteData()
        };

        return (controller, addresses);
    }

    private static Address NewAddress(string name, string phone, string line, bool isDefault) => new()
    {
        Id = name == "An" ? 1 : 2,
        UserId = "u1",
        RecipientName = name,
        RecipientPhone = phone,
        AddressLine = line,
        IsDefault = isDefault
    };

    [Fact]
    public async Task Checkout_WithDefaultAddress_PrefillsShippingFields()
    {
        var (controller, addresses) = CreateController();
        addresses.Setup(a => a.GetAddressesAsync("u1"))
            .ReturnsAsync(new[] { NewAddress("An", "0901234567", "Số 1 Nguyễn Trãi", true) });

        var result = await controller.Checkout();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CheckoutPreview", view.ViewName);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);
        Assert.Equal("An", model.RecipientName);
        Assert.Equal("0901234567", model.RecipientPhone);
        Assert.Equal("Số 1 Nguyễn Trãi", model.ShippingAddress);
        Assert.Equal(1, model.SavedAddressId);
    }

    [Fact]
    public async Task Checkout_WithSavedAddresses_ExposesOptions()
    {
        var (controller, addresses) = CreateController();
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(new[]
        {
            NewAddress("An", "0901234567", "Số 1 Nguyễn Trãi", true),
            NewAddress("Bình", "0912345678", "Số 2 Lê Lợi", false)
        });

        var result = await controller.Checkout();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);
        Assert.Equal(2, model.SavedAddresses.Count);
        Assert.Equal("An", model.SavedAddresses[0].RecipientName);
        Assert.Equal("Bình", model.SavedAddresses[1].RecipientName);
        Assert.Contains("Số 2 Lê Lợi", model.SavedAddresses[1].Display);
    }

    [Fact]
    public async Task Checkout_WithoutAddresses_LeavesFieldsEmpty()
    {
        var (controller, addresses) = CreateController();
        addresses.Setup(a => a.GetAddressesAsync("u1"))
            .ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);
        Assert.Equal(string.Empty, model.RecipientName);
        Assert.Equal(string.Empty, model.RecipientPhone);
        Assert.Equal(string.Empty, model.ShippingAddress);
        Assert.Null(model.SavedAddressId);
        Assert.Empty(model.SavedAddresses);
    }

    [Fact]
    public async Task Checkout_WithValidCoupon_ShowsDiscountAndPayableTotal()
    {
        var coupons = new Mock<ICouponService>();
        coupons.Setup(c => c.ValidateAsync("SAVE10", 10_000_000m))
            .ReturnsAsync(new CouponValidationResult(true, null, "SAVE10", 1_000_000m, "Giảm 10%"));
        var (controller, addresses) = CreateController(coupons);
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout("SAVE10");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);
        Assert.Equal("SAVE10", model.CouponCode);
        Assert.Equal(1_000_000m, model.DiscountAmount);
        Assert.Equal(9_000_000m, model.PayableTotal);
        Assert.Null(model.CouponError);
        Assert.Equal("Giảm 10%", model.CouponDescription);
    }

    [Fact]
    public async Task Checkout_WithInvalidCoupon_ShowsErrorAndNoDiscount()
    {
        var coupons = new Mock<ICouponService>();
        coupons.Setup(c => c.ValidateAsync("EXPIRED", 10_000_000m))
            .ReturnsAsync(new CouponValidationResult(false, "Mã giảm giá đã hết hạn.", null, 0m, null));
        var (controller, addresses) = CreateController(coupons);
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout("EXPIRED");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);
        Assert.Equal("Mã giảm giá đã hết hạn.", model.CouponError);
        Assert.Equal(0m, model.DiscountAmount);
        Assert.Equal(10_000_000m, model.PayableTotal);
    }

    [Fact]
    public async Task Checkout_WhenActiveMethods_SelectsFirstAndComputesFees()
    {
        var shipping = new Mock<IShippingCalculator>();
        shipping.Setup(s => s.GetActiveMethodsAsync()).ReturnsAsync(new[]
        {
            new ShippingMethod { Id = 2, Name = "Nhanh", Fee = 30_000m, IsActive = true, DisplayOrder = 1 },
            new ShippingMethod { Id = 1, Name = "Tiêu chuẩn", Fee = 60_000m, IsActive = true, DisplayOrder = 2 }
        });
        shipping.Setup(s => s.QuoteAsync(2, 10_000_000m))
            .ReturnsAsync(new ShippingQuote(2, "Nhanh", 30_000m, 0.1m, 1_000_000m, 11_030_000m));
        var (controller, addresses) = CreateController(shipping: shipping);
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout();

        var model = Assert.IsType<CheckoutViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.ShippingMethodId);
        Assert.Equal(2, model.ShippingMethods.Count);
        Assert.Equal(30_000m, model.ShippingFee);
        Assert.Equal(0.1m, model.TaxRate);
        Assert.Equal(1_000_000m, model.TaxAmount);
        Assert.Equal(11_030_000m, model.PayableTotal);
        Assert.Null(model.ShippingError);
    }

    [Fact]
    public async Task Checkout_WhenNoMethods_ShowsShippingErrorAndZeroFees()
    {
        var (controller, addresses) = CreateController();
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout();

        var model = Assert.IsType<CheckoutViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.ShippingMethods);
        Assert.Null(model.ShippingMethodId);
        Assert.Equal(0m, model.ShippingFee);
        Assert.Equal(0m, model.TaxAmount);
        Assert.Equal("Hiện chưa có phương thức vận chuyển khả dụng.", model.ShippingError);
        Assert.Equal(10_000_000m, model.PayableTotal);
    }

    [Fact]
    public async Task Checkout_WithCouponAndShipping_QuotesDiscountedSubtotal()
    {
        var coupons = new Mock<ICouponService>();
        coupons.Setup(c => c.ValidateAsync("SAVE10", 10_000_000m))
            .ReturnsAsync(new CouponValidationResult(true, null, "SAVE10", 1_000_000m, "Giảm 10%"));
        var shipping = new Mock<IShippingCalculator>();
        shipping.Setup(s => s.GetActiveMethodsAsync()).ReturnsAsync(new[]
        {
            new ShippingMethod { Id = 1, Name = "Tiêu chuẩn", Fee = 30_000m, IsActive = true, DisplayOrder = 1 }
        });
        shipping.Setup(s => s.QuoteAsync(1, 9_000_000m))
            .ReturnsAsync(new ShippingQuote(1, "Tiêu chuẩn", 30_000m, 0.1m, 900_000m, 9_930_000m));
        var (controller, addresses) = CreateController(coupons, shipping);
        addresses.Setup(a => a.GetAddressesAsync("u1")).ReturnsAsync(Array.Empty<Address>());

        var result = await controller.Checkout("SAVE10");

        var model = Assert.IsType<CheckoutViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(9_930_000m, model.PayableTotal);
        shipping.Verify(s => s.QuoteAsync(1, 9_000_000m), Times.Once);
    }
}
