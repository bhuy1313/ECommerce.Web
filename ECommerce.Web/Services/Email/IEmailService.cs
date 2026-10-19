namespace ECommerce.Web.Services.Email;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string to, string confirmationUrl);
    Task SendPasswordResetAsync(string to, string resetUrl);
    Task SendOrderConfirmationAsync(int orderId, string orderUrl);
}
