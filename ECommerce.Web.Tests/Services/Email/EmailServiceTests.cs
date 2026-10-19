using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services.Email;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.Web.Tests.Services.Email;

public class EmailServiceTests
{
    private static EmailService Create(
        Mock<IEmailTemplateRenderer> renderer, Mock<IEmailSender> sender, ApplicationDbContext db)
        => new(renderer.Object, sender.Object, db, NullLogger<EmailService>.Instance);

    [Fact]
    public async Task SendEmailConfirmationAsync_UsesConfirmationTemplateAndSubject()
    {
        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer.Setup(r => r.RenderAsync("~/Views/Emails/ConfirmEmail.cshtml", It.IsAny<EmailLinkViewModel>()))
            .ReturnsAsync("<a href='https://shop.test/confirm'>confirm</a>");
        var sender = new Mock<IEmailSender>();
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _db = db;

        await Create(renderer, sender, db)
            .SendEmailConfirmationAsync("buyer@test.local", "https://shop.test/confirm");

        sender.Verify(s => s.SendAsync(
            "buyer@test.local",
            "Xác thực email của bạn – TechNova",
            "<a href='https://shop.test/confirm'>confirm</a>",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetAsync_UsesResetTemplateAndSubject()
    {
        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer.Setup(r => r.RenderAsync("~/Views/Emails/ResetPassword.cshtml", It.IsAny<EmailLinkViewModel>()))
            .ReturnsAsync("<p>reset</p>");
        var sender = new Mock<IEmailSender>();
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _db = db;

        await Create(renderer, sender, db)
            .SendPasswordResetAsync("buyer@test.local", "https://shop.test/reset");

        sender.Verify(s => s.SendAsync(
            "buyer@test.local",
            "Đặt lại mật khẩu – TechNova",
            "<p>reset</p>",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_LoadsOrderWithItemsAndSendsToBuyer()
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _db = db;
        var user = new ApplicationUser { UserName = "buyer@test.local", Email = "buyer@test.local" };
        db.Users.Add(user);
        var order = new Order
        {
            UserId = user.Id,
            RecipientName = "Nguyen Van A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            TotalAmount = 200000
        };
        order.Items.Add(new OrderItem { ProductName = "Áo thun", Quantity = 2, UnitPrice = 100000 });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        OrderConfirmationEmailViewModel? captured = null;
        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer.Setup(r => r.RenderAsync("~/Views/Emails/OrderConfirmation.cshtml", It.IsAny<OrderConfirmationEmailViewModel>()))
            .Callback<string, OrderConfirmationEmailViewModel>((_, model) => captured = model)
            .ReturnsAsync("<p>order</p>");
        var sender = new Mock<IEmailSender>();

        await Create(renderer, sender, db)
            .SendOrderConfirmationAsync(order.Id, "https://shop.test/Orders/Details/1");

        Assert.NotNull(captured);
        Assert.Equal(order.Id, captured!.Order.Id);
        Assert.Equal("Áo thun", Assert.Single(captured.Order.Items).ProductName);
        Assert.Equal("https://shop.test/Orders/Details/1", captured.OrderUrl);
        sender.Verify(s => s.SendAsync(
            "buyer@test.local",
            $"Xác nhận đơn hàng #{order.Id} – TechNova",
            "<p>order</p>",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_WhenOrderMissing_DoesNotSendOrThrow()
    {
        var renderer = new Mock<IEmailTemplateRenderer>();
        var sender = new Mock<IEmailSender>();
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _db = db;

        await Create(renderer, sender, db).SendOrderConfirmationAsync(999, "https://shop.test/o");

        sender.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_WhenBuyerHasNoEmail_DoesNotSend()
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _db = db;
        var user = new ApplicationUser { UserName = "no-mail", Email = null };
        db.Users.Add(user);
        var order = new Order { UserId = user.Id, TotalAmount = 1 };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var sender = new Mock<IEmailSender>();
        var renderer = new Mock<IEmailTemplateRenderer>();

        await Create(renderer, sender, db).SendOrderConfirmationAsync(order.Id, "https://shop.test/o");

        sender.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
