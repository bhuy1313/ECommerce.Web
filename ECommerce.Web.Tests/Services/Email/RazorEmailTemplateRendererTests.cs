using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace ECommerce.Web.Tests.Services.Email;

public class RazorEmailTemplateRendererTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ECommerce.Web";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = LocateWebProject();
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(LocateWebProject());
        public string WebRootPath { get; set; } = Path.Combine(LocateWebProject(), "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } =
            new PhysicalFileProvider(Path.Combine(LocateWebProject(), "wwwroot"));
    }

    private static string LocateWebProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var projectDir = Path.Combine(dir.FullName, "ECommerce.Web");
            if (Directory.Exists(Path.Combine(projectDir, "Views")))
            {
                return projectDir;
            }
        }

        throw new InvalidOperationException("Could not locate the ECommerce.Web project folder.");
    }

    private static async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(IEmailTemplateRenderer).Assembly);
        services.AddHttpContextAccessor();
        var diagnosticListener = new System.Diagnostics.DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton<System.Diagnostics.DiagnosticSource>(diagnosticListener);
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        return await scope.ServiceProvider
            .GetRequiredService<IEmailTemplateRenderer>()
            .RenderAsync(viewPath, model);
    }

    [Fact]
    public async Task RenderAsync_ConfirmEmail_ContainsEscapedUrl()
    {
        var html = await RenderAsync(
            "~/Views/Emails/ConfirmEmail.cshtml",
            new EmailLinkViewModel("https://shop.test/Account/ConfirmEmail?userId=u1&code=abc"));

        Assert.Contains("https://shop.test/Account/ConfirmEmail?userId=u1&amp;code=abc", html);
    }

    [Fact]
    public async Task RenderAsync_ResetPassword_ContainsEscapedUrl()
    {
        var html = await RenderAsync(
            "~/Views/Emails/ResetPassword.cshtml",
            new EmailLinkViewModel("https://shop.test/Account/ResetPassword?userId=u1&code=xyz"));

        Assert.Contains("https://shop.test/Account/ResetPassword?userId=u1&amp;code=xyz", html);
    }

    [Fact]
    public async Task RenderAsync_OrderConfirmation_ContainsOrderSummary()
    {
        var order = new Order
        {
            Id = 42,
            RecipientName = "Nguyen Van A",
            RecipientPhone = "0901234567",
            ShippingAddress = "12 Le Loi",
            TotalAmount = 200000
        };
        order.Items.Add(new OrderItem { ProductName = "Áo thun", Quantity = 2, UnitPrice = 100000 });

        var html = await RenderAsync(
            "~/Views/Emails/OrderConfirmation.cshtml",
            new OrderConfirmationEmailViewModel(order, "https://shop.test/Orders/Details/42"));

        Assert.Contains("#42", html);
        Assert.Contains("Áo thun", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains(order.TotalAmount.ToString("N0"), html);
        Assert.Contains("https://shop.test/Orders/Details/42", html);
    }
}
