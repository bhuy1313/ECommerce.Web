using ECommerce.Web.Services.Email;
using Microsoft.Extensions.Logging;

namespace ECommerce.Web.Tests.Services.Email;

public class LogEmailSenderTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task SendAsync_LogsRecipientSubjectAndBody()
    {
        var logger = new CapturingLogger<LogEmailSender>();
        var sender = new LogEmailSender(logger);

        await sender.SendAsync("buyer@test.local", "Xin chào", "<p>Nội dung</p>");

        var text = string.Join("\n", logger.Messages);
        Assert.Contains("=== EMAIL (dev) ===", text);
        Assert.Contains("To: buyer@test.local", text);
        Assert.Contains("Subject: Xin chào", text);
        Assert.Contains("<p>Nội dung</p>", text);
    }
}
