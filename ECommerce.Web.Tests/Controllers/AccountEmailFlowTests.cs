using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

public class AccountEmailFlowTests
{
    private sealed record Harness(
        AccountController Controller,
        Mock<UserManager<ApplicationUser>> Users,
        Mock<SignInManager<ApplicationUser>> SignIn,
        Mock<IEmailService> Email);

    private static Harness CreateController()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var users = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signIn = new Mock<SignInManager<ApplicationUser>>(
            users.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);
        var email = new Mock<IEmailService>();
        var controller = new AccountController(
            users.Object, signIn.Object, Mock.Of<ICartService>(),
            email.Object, Mock.Of<INotyfService>(), Mock.Of<ILogger<AccountController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://shop.test/link");
        controller.Url = url.Object;
        return new Harness(controller, users, signIn, email);
    }

    private static ApplicationUser NewUser() => new()
    {
        Id = "u1",
        UserName = "buyer@test.local",
        Email = "buyer@test.local"
    };

    [Fact]
    public async Task Register_WhenCreated_SendsOneConfirmationEmail()
    {
        var h = CreateController();
        h.Users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        h.Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        h.Users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("raw+token/==");
        h.SignIn.Setup(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false, null)).Returns(Task.CompletedTask);
        h.Email.Setup(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await h.Controller.Register(new RegisterViewModel
        {
            Email = "buyer@test.local",
            Password = "Valid@123",
            ConfirmPassword = "Valid@123"
        });

        Assert.IsType<RedirectToActionResult>(result);
        h.Email.Verify(e => e.SendEmailConfirmationAsync(
            "buyer@test.local", "https://shop.test/link"), Times.Once);
    }

    [Fact]
    public async Task Register_WhenEmailFails_StillSignsInAndRedirects()
    {
        var h = CreateController();
        h.Users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        h.Users.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        h.Users.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        h.Users.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("token");
        h.SignIn.Setup(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false, null)).Returns(Task.CompletedTask);
        h.Email.Setup(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var result = await h.Controller.Register(new RegisterViewModel
        {
            Email = "buyer@test.local",
            Password = "Valid@123",
            ConfirmPassword = "Valid@123"
        });

        Assert.IsType<RedirectToActionResult>(result);
        h.SignIn.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false, null), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_ValidEncodedCode_ConfirmsWithRawToken()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ConfirmEmailAsync(user, "raw+token/==")).ReturnsAsync(IdentityResult.Success);

        var result = await h.Controller.ConfirmEmail("u1", IdentityTokenCodec.Encode("raw+token/=="));

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ConfirmEmailViewModel>(view.Model);
        Assert.True(model.Success);
        Assert.Equal("Email của bạn đã được xác thực thành công.", model.Message);
    }

    [Fact]
    public async Task ConfirmEmail_MalformedCode_DoesNotThrowAndReportsInvalid()
    {
        var h = CreateController();

        var result = await h.Controller.ConfirmEmail("u1", "%%%");

        var model = Assert.IsType<ConfirmEmailViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.Success);
        h.Users.Verify(u => u.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_TokenRejected_ReportsInvalid()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ConfirmEmailAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "expired" }));

        var result = await h.Controller.ConfirmEmail("u1", IdentityTokenCodec.Encode("stale"));

        var model = Assert.IsType<ConfirmEmailViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.Success);
    }

    [Fact]
    public async Task Resend_WhenUserUnconfirmed_SendsAndRedirectsToLogin()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        h.Users.Setup(u => u.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        h.Users.Setup(u => u.GenerateEmailConfirmationTokenAsync(user)).ReturnsAsync("token");
        h.Email.Setup(e => e.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await h.Controller.ResendEmailConfirmation(
            new ResendConfirmationViewModel { Email = user.Email! });

        Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        h.Email.Verify(e => e.SendEmailConfirmationAsync(user.Email!, "https://shop.test/link"), Times.Once);
    }

    [Fact]
    public async Task Resend_WhenUserMissing_SendsNothingAndStillRedirects()
    {
        var h = CreateController();
        h.Users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var result = await h.Controller.ResendEmailConfirmation(
            new ResendConfirmationViewModel { Email = "nobody@test.local" });

        Assert.Equal("Login", Assert.IsType<RedirectToActionResult>(result).ActionName);
        h.Email.Verify(e => e.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SendsNothingAndShowsConfirmation()
    {
        var h = CreateController();
        h.Users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var result = await h.Controller.ForgotPassword(new ForgotPasswordViewModel { Email = "nobody@test.local" });

        Assert.Equal("ForgotPasswordConfirmation", Assert.IsType<RedirectToActionResult>(result).ActionName);
        h.Email.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsResetLinkAndShowsConfirmation()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        h.Users.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("raw+token/==");
        h.Email.Setup(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await h.Controller.ForgotPassword(new ForgotPasswordViewModel { Email = user.Email! });

        Assert.Equal("ForgotPasswordConfirmation", Assert.IsType<RedirectToActionResult>(result).ActionName);
        h.Email.Verify(e => e.SendPasswordResetAsync(user.Email!, "https://shop.test/link"), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ValidEncodedCode_ResetsWithRawToken()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ResetPasswordAsync(user, "raw+token/==", "New@1234"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await h.Controller.ResetPassword(new ResetPasswordViewModel
        {
            UserId = "u1",
            Code = IdentityTokenCodec.Encode("raw+token/=="),
            Password = "New@1234",
            ConfirmPassword = "New@1234"
        });

        Assert.Equal("ResetPasswordConfirmation", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task ResetPassword_MalformedCode_DoesNotThrowAndReRenders()
    {
        var h = CreateController();

        var result = await h.Controller.ResetPassword(new ResetPasswordViewModel
        {
            UserId = "u1",
            Code = "%%%",
            Password = "New@1234",
            ConfirmPassword = "New@1234"
        });

        Assert.IsType<ViewResult>(result);
        h.Users.Verify(u => u.ResetPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_TokenRejected_ReRendersWithError()
    {
        var h = CreateController();
        var user = NewUser();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ResetPasswordAsync(user, "stale", "New@1234"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "expired" }));

        var result = await h.Controller.ResetPassword(new ResetPasswordViewModel
        {
            UserId = "u1",
            Code = IdentityTokenCodec.Encode("stale"),
            Password = "New@1234",
            ConfirmPassword = "New@1234"
        });

        Assert.IsType<ViewResult>(result);
        Assert.True(h.Controller.ModelState.ContainsKey(string.Empty));
    }
}
