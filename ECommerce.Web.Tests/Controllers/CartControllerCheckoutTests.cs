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
    private static (CartController Controller, Mock<IAddressService> Addresses) CreateController()
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
        var controller = new CartController(cart.Object, Mock.Of<INotyfService>(), addresses.Object);
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
}
