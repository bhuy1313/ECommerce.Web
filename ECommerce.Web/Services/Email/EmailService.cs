using ECommerce.Web.Data;
using ECommerce.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Web.Services.Email;

public sealed class EmailService : IEmailService
{
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailSender _sender;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IEmailTemplateRenderer renderer,
        IEmailSender sender,
        ApplicationDbContext db,
        ILogger<EmailService> logger)
    {
        _renderer = renderer;
        _sender = sender;
        _db = db;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(string to, string confirmationUrl) =>
        SendAsync(
            to,
            "Xác thực email của bạn – TechNova",
            "~/Views/Emails/ConfirmEmail.cshtml",
            new EmailLinkViewModel(confirmationUrl));

    public Task SendPasswordResetAsync(string to, string resetUrl) =>
        SendAsync(
            to,
            "Đặt lại mật khẩu – TechNova",
            "~/Views/Emails/ResetPassword.cshtml",
            new EmailLinkViewModel(resetUrl));

    public async Task SendOrderConfirmationAsync(int orderId, string orderUrl)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found; skipping confirmation email.", orderId);
            return;
        }

        var email = order.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Order {OrderId} has no buyer email; skipping confirmation email.", orderId);
            return;
        }

        await SendAsync(
            email,
            $"Xác nhận đơn hàng #{order.Id} – TechNova",
            "~/Views/Emails/OrderConfirmation.cshtml",
            new OrderConfirmationEmailViewModel(order, orderUrl));
    }

    private async Task SendAsync<TModel>(string to, string subject, string viewPath, TModel model)
    {
        var html = await _renderer.RenderAsync(viewPath, model);
        await _sender.SendAsync(to, subject, html);
    }
}
