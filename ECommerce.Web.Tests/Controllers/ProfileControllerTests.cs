using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

public class ProfileControllerTests
{
    private const string NotFound = "Không tìm thấy địa chỉ.";

    private sealed record Harness(
        ProfileController Controller,
        Mock<IAddressService> Addresses,
        Mock<INotyfService> Notyf);

    private static Harness CreateController()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var users = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("u1");
        var addresses = new Mock<IAddressService>();
        var notyf = new Mock<INotyfService>();
        var controller = new ProfileController(users.Object, addresses.Object, notyf.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return new Harness(controller, addresses, notyf);
    }

    private static Address NewAddress(string name, bool isDefault = false) => new()
    {
        Id = name == "An" ? 1 : 2,
        UserId = "u1",
        RecipientName = name,
        RecipientPhone = "0901234567",
        AddressLine = "1 Đường Láng, Hà Nội",
        IsDefault = isDefault
    };

    private static AddressFormViewModel NewForm() => new()
    {
        RecipientName = "An",
        RecipientPhone = "0901234567",
        AddressLine = "1 Đường Láng, Hà Nội"
    };

    [Fact]
    public async Task Index_ReturnsViewWithAddresses()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.GetAddressesAsync("u1"))
            .ReturnsAsync(new[] { NewAddress("An", true), NewAddress("Bình") });

        var result = await h.Controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProfileViewModel>(view.Model);
        Assert.Equal(2, model.Addresses.Count);
        Assert.True(model.Addresses[0].IsDefault);
    }

    [Fact]
    public async Task Add_WhenValid_AddsAndRedirects()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.AddAddressAsync("u1", "An", "0901234567", "1 Đường Láng, Hà Nội", false))
            .ReturnsAsync((true, "Đã thêm địa chỉ mới."));

        var result = await h.Controller.Add(NewForm());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);
        h.Addresses.Verify(a => a.AddAddressAsync("u1", "An", "0901234567", "1 Đường Láng, Hà Nội", false), Times.Once);
        h.Notyf.Verify(n => n.Success("Đã thêm địa chỉ mới."), Times.Once);
    }

    [Fact]
    public async Task Add_WhenServiceRejects_ShowsErrorAndRedirects()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.AddAddressAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, NotFound));

        var result = await h.Controller.Add(NewForm());

        Assert.IsType<RedirectToActionResult>(result);
        h.Notyf.Verify(n => n.Error(NotFound), Times.Once);
        h.Notyf.Verify(n => n.Success(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Add_WhenModelInvalid_RerendersWithAddresses()
    {
        var h = CreateController();
        h.Controller.ModelState.AddModelError(nameof(AddressFormViewModel.RecipientName), "required");
        h.Addresses.Setup(a => a.GetAddressesAsync("u1"))
            .ReturnsAsync(new[] { NewAddress("Bình", true) });

        var result = await h.Controller.Add(NewForm());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        var model = Assert.IsType<ProfileViewModel>(view.Model);
        Assert.Single(model.Addresses);
        Assert.Equal("An", model.NewAddress.RecipientName);
        h.Addresses.Verify(a => a.AddAddressAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SetDefault_WhenOwned_RedirectsAndToasts()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.SetDefaultAddressAsync("u1", 7))
            .ReturnsAsync((true, "Đã đặt làm địa chỉ mặc định."));

        var result = await h.Controller.SetDefault(7);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);
        h.Notyf.Verify(n => n.Success("Đã đặt làm địa chỉ mặc định."), Times.Once);
    }

    [Fact]
    public async Task SetDefault_WhenForeign_ShowsError()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.SetDefaultAddressAsync("u1", 7)).ReturnsAsync((false, NotFound));

        var result = await h.Controller.SetDefault(7);

        Assert.IsType<RedirectToActionResult>(result);
        h.Notyf.Verify(n => n.Error(NotFound), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenOwned_RedirectsAndToasts()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.DeleteAddressAsync("u1", 7))
            .ReturnsAsync((true, "Đã xóa địa chỉ."));

        var result = await h.Controller.Delete(7);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);
        h.Notyf.Verify(n => n.Success("Đã xóa địa chỉ."), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenForeign_ShowsError()
    {
        var h = CreateController();
        h.Addresses.Setup(a => a.DeleteAddressAsync("u1", 7)).ReturnsAsync((false, NotFound));

        var result = await h.Controller.Delete(7);

        Assert.IsType<RedirectToActionResult>(result);
        h.Notyf.Verify(n => n.Error(NotFound), Times.Once);
    }
}
