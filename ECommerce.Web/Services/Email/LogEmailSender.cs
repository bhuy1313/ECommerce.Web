using Microsoft.Extensions.Logging;

namespace ECommerce.Web.Services.Email;

public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "=== EMAIL (dev) ===\nTo: {To}\nSubject: {Subject}\nBody:\n{Body}",
            to,
            subject,
            htmlBody);

        return Task.CompletedTask;
    }
}
