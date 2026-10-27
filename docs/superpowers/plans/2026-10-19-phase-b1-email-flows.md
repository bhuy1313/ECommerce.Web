# B1 — Email Flows (dev infrastructure) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make email confirmation, forgot/reset password and order confirmation work end-to-end in Development through a logging email sink and Razor templates.

**Architecture:** `IEmailSender` (dev sink logs the message) + Razor-to-string renderer + `IEmailService` that owns subjects/template paths; Identity tokens are Base64Url-encoded before entering URLs; controllers build links with `Url.Action` and isolate every send behind try/catch.

**Tech Stack:** ASP.NET Core MVC (.NET 10), ASP.NET Core Identity, Razor views, EF Core (SQLite in tests), xUnit + Moq.

**Spec:** `docs/superpowers/specs/2026-10-19-phase-b1-email-flows-design.md`

## Global Constraints

- Target `net10.0`; no new NuGet packages.
- Build must stay at **0 warnings** (`dotnet build ECommerce.Web.slnx --no-incremental`).
- All async work is `async Task` + `await` (never `.Result` / `.GetAwaiter().GetResult()`).
- Every POST action: `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("auth")]`.
- UI copy is Vietnamese; prices render as `value.ToString("N0") + " ₫"`.
- `SignIn.RequireConfirmedEmail` stays **false** — do not add it.
- Only `LogEmailSender` is registered in this phase; do not add provider config or packages.
- Identity tokens must never appear in a URL raw — always `IdentityTokenCodec.Encode`.
- An email-send failure must never produce a 500 on Register or Checkout.
- Tests live in `ECommerce.Web.Tests/`; controller tests mock `UserManager`/`SignInManager` (see `CheckoutControllerTests.cs:24-52`); DB-backed tests use `TestDbContextFactory`.

## Review Focus

1. A confirmation/reset code containing `+`, `/` or `=` is mishandled in the URL (space or double-encoding) → link always invalid. (Tests: Task 2 codec, Task 4 `ConfirmEmail` round-trip, Task 5 reset.)
2. The mail sender throws during Register or Checkout → user sees a 500 or loses the order. (Tests: Task 4 register, Task 6 checkout.)
3. ForgotPassword with an unknown address behaves differently from a known one → account enumeration. (Test: Task 5.)
4. A tampered/garbage `code` raises `FormatException` → 500 instead of a friendly message. (Tests: Task 2 codec, Task 4 malformed, Task 5 malformed.)
5. An expired or rejected Identity token is reported as success. (Tests: Task 4 rejected token, Task 5 rejected token.)

---

### Task 1: Log email sender

**Files:**
- Create: `ECommerce.Web/Services/Email/IEmailSender.cs`
- Create: `ECommerce.Web/Services/Email/LogEmailSender.cs`
- Modify: `ECommerce.Web/Program.cs` (DI, after `AddHttpContextAccessor()` at line 32)
- Test: `ECommerce.Web.Tests/Services/Email/LogEmailSenderTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `namespace ECommerce.Web.Services.Email; public interface IEmailSender { Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default); }` and `public sealed class LogEmailSender : IEmailSender`.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run it and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~LogEmailSenderTests"`
Expected: compile error `The type or namespace name 'IEmailSender' could not be found`.

- [ ] **Step 3: Implement the sender and register it**

`IEmailSender.cs` is the interface above. `LogEmailSender` stores `ILogger<LogEmailSender>`,
and `SendAsync` writes one Information entry with this exact shape (newlines inside the
template):

```
=== EMAIL (dev) ===
To: {To}
Subject: {Subject}
Body:
{Body}
```

then returns `Task.CompletedTask`. In `Program.cs`, next to the other `AddScoped` service
registrations:

```csharp
builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
```

with `using ECommerce.Web.Services.Email;` added to the file.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~LogEmailSenderTests"`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add ECommerce.Web/Services/Email/IEmailSender.cs ECommerce.Web/Services/Email/LogEmailSender.cs ECommerce.Web/Program.cs ECommerce.Web.Tests/Services/Email/LogEmailSenderTests.cs
git commit -m "feat: add email sender abstraction with a dev log sink"
```

---

### Task 2: Token codec, Razor template renderer and the confirmation template

**Files:**
- Create: `ECommerce.Web/Services/Email/IdentityTokenCodec.cs`
- Create: `ECommerce.Web/Services/Email/IEmailTemplateRenderer.cs`
- Create: `ECommerce.Web/Services/Email/RazorEmailTemplateRenderer.cs`
- Create: `ECommerce.Web/Models/ViewModels/EmailViewModels.cs`
- Create: `ECommerce.Web/Views/Emails/ConfirmEmail.cshtml`
- Modify: `ECommerce.Web/Program.cs` (DI)
- Test: `ECommerce.Web.Tests/Services/Email/IdentityTokenCodecTests.cs`
- Test: `ECommerce.Web.Tests/Services/Email/RazorEmailTemplateRendererTests.cs`

**Interfaces:**
- Consumes: `IHttpContextAccessor` (registered at `Program.cs:32`).
- Produces:
  - `public static class IdentityTokenCodec { public static string Encode(string token); public static bool TryDecode(string? code, out string token); }`
  - `public interface IEmailTemplateRenderer { Task<string> RenderAsync<TModel>(string viewPath, TModel model); }`
  - `public sealed record EmailLinkViewModel(string Url);`
  - Template path constant used by later tasks: `"~/Views/Emails/ConfirmEmail.cshtml"`.

- [ ] **Step 1: Write the failing codec tests**

```csharp
using ECommerce.Web.Services.Email;

namespace ECommerce.Web.Tests.Services.Email;

public class IdentityTokenCodecTests
{
    [Fact]
    public void EncodeThenTryDecode_RoundTripsUrlHostileToken()
    {
        const string raw = "CfDJ8+abc/def==";

        var code = IdentityTokenCodec.Encode(raw);

        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
        Assert.True(IdentityTokenCodec.TryDecode(code, out var decoded));
        Assert.Equal(raw, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%%%")]
    [InlineData("a*b")]
    public void TryDecode_RejectsMissingOrNonBase64UrlInput(string? code)
    {
        Assert.False(IdentityTokenCodec.TryDecode(code, out var token));
        Assert.Equal(string.Empty, token);
    }
}
```

- [ ] **Step 2: Run it and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~IdentityTokenCodecTests"`
Expected: compile error `The type or namespace name 'IdentityTokenCodec' could not be found`.

- [ ] **Step 3: Implement the codec**

`Encode` = `WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))` (namespace
`Microsoft.AspNetCore.WebUtilities`). `TryDecode` rejects null/whitespace, returns `false`
on `FormatException` from `WebEncoders.Base64UrlDecode`, sets `token = string.Empty` on
failure, and returns `false` when the decoded string is empty.

- [ ] **Step 4: Write the failing renderer test**

```csharp
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
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
}
```

- [ ] **Step 5: Run it and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~RazorEmailTemplateRendererTests"`
Expected: compile errors for `IEmailTemplateRenderer` / `EmailLinkViewModel`.

- [ ] **Step 6: Implement the renderer and the confirm template**

`RazorEmailTemplateRenderer` ctor: `(IHttpContextAccessor httpContextAccessor, IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IModelMetadataProvider metadataProvider)`.

`RenderAsync`:

```csharp
var httpContext = _httpContextAccessor.HttpContext
    ?? throw new InvalidOperationException("Email templates require an active HTTP request.");
var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: false);
if (!viewResult.Success)
{
    throw new InvalidOperationException(
        $"Email view '{viewPath}' not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");
}
using var writer = new StringWriter();
var viewData = new ViewDataDictionary<TModel>(_metadataProvider, new ModelStateDictionary())
{
    Model = model
};
var viewContext = new ViewContext(
    actionContext,
    viewResult.View,
    viewData,
    new TempDataDictionary(httpContext, _tempDataProvider),
    writer,
    new HtmlHelperOptions());
await viewResult.View.RenderAsync(viewContext);
return writer.ToString();
```

`Views/Emails/ConfirmEmail.cshtml`: `@model ECommerce.Web.Models.ViewModels.EmailLinkViewModel`,
no layout, inline CSS, Vietnamese copy, heading "Xác thực email của bạn", body text
"Cảm ơn bạn đã đăng ký TechNova. Nhấn vào nút bên dưới để xác thực địa chỉ email.",
a button `<a href="@Model.Url">Xác thực email</a>`, and the note
"Liên kết có hiệu lực trong 1 ngày. Nếu bạn không tạo tài khoản, hãy bỏ qua email này."

Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
```

- [ ] **Step 7: Run both test classes**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~IdentityTokenCodecTests|FullyQualifiedName~RazorEmailTemplateRendererTests"`
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Services/Email/IdentityTokenCodec.cs ECommerce.Web/Services/Email/IEmailTemplateRenderer.cs ECommerce.Web/Services/Email/RazorEmailTemplateRenderer.cs ECommerce.Web/Models/ViewModels/EmailViewModels.cs ECommerce.Web/Views/Emails/ConfirmEmail.cshtml ECommerce.Web/Program.cs ECommerce.Web.Tests/Services/Email/IdentityTokenCodecTests.cs ECommerce.Web.Tests/Services/Email/RazorEmailTemplateRendererTests.cs
git commit -m "feat: render Razor email templates and encode identity tokens for URLs"
```

---

### Task 3: Email service + reset and order templates

**Files:**
- Create: `ECommerce.Web/Services/Email/IEmailService.cs`
- Create: `ECommerce.Web/Services/Email/EmailService.cs`
- Modify: `ECommerce.Web/Models/ViewModels/EmailViewModels.cs` (add order model)
- Create: `ECommerce.Web/Views/Emails/ResetPassword.cshtml`
- Create: `ECommerce.Web/Views/Emails/OrderConfirmation.cshtml`
- Modify: `ECommerce.Web/Program.cs` (DI)
- Test: `ECommerce.Web.Tests/Services/Email/EmailServiceTests.cs`

**Interfaces:**
- Consumes: `IEmailSender`, `IEmailTemplateRenderer`, `EmailLinkViewModel`, `ApplicationDbContext`.
- Produces:

```csharp
namespace ECommerce.Web.Services.Email;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string to, string confirmationUrl);
    Task SendPasswordResetAsync(string to, string resetUrl);
    Task SendOrderConfirmationAsync(int orderId, string orderUrl);
}

public sealed record OrderConfirmationEmailViewModel(ECommerce.Web.Models.Entities.Order Order, string OrderUrl);
```

- Exact subjects: `"Xác thực email của bạn – TechNova"`, `"Đặt lại mật khẩu – TechNova"`, `$"Xác nhận đơn hàng #{order.Id} – TechNova"`.

- [ ] **Step 1: Write the failing service tests**

```csharp
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
```

- [ ] **Step 2: Run it and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~EmailServiceTests"`
Expected: compile errors for `EmailService` / `IEmailService` / `OrderConfirmationEmailViewModel`.

- [ ] **Step 3: Implement the service and templates**

`EmailService` ctor `(IEmailTemplateRenderer renderer, IEmailSender sender, ApplicationDbContext db, ILogger<EmailService> logger)`.

- Confirmation and reset: render the fixed template path with `new EmailLinkViewModel(url)` and send the fixed subject.
- Order: `_db.Orders.Include(o => o.Items).Include(o => o.User).AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId)`;
  when null or `string.IsNullOrWhiteSpace(order.User?.Email)`, `LogWarning` and return;
  otherwise render with `new OrderConfirmationEmailViewModel(order, orderUrl)` and send to `order.User.Email`.

`ResetPassword.cshtml`: heading "Đặt lại mật khẩu", text "Chúng tôi nhận được yêu cầu
đặt lại mật khẩu cho tài khoản TechNova của bạn.", button `href="@Model.Url"` text
"Đặt lại mật khẩu", note "Liên kết có hiệu lực trong 1 ngày. Nếu bạn không yêu cầu, hãy
bỏ qua email này."

`OrderConfirmation.cshtml`: heading `Xác nhận đơn hàng #@Model.Order.Id`, table rows over
`Model.Order.Items` showing `ProductName`, `Quantity`, `UnitPrice.ToString("N0") ₫`,
`(Quantity * UnitPrice).ToString("N0") ₫`; total `Model.Order.TotalAmount.ToString("N0") ₫`;
recipient block with `RecipientName`, `RecipientPhone`, `ShippingAddress`;
payment line `@(Model.Order.Method == PaymentMethod.Cod ? "Thanh toán khi nhận hàng (COD)" : "VNPay")`;
button `href="@Model.OrderUrl"` text "Xem chi tiết đơn hàng". Add
`@using ECommerce.Web.Models.Entities.Enums` at the top of the file.

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<IEmailService, EmailService>();
```

- [ ] **Step 4: Run the service tests**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~EmailServiceTests"`
Expected: 5 passed.

- [ ] **Step 5: Extend the renderer test to cover the two new templates**

Add to `RazorEmailTemplateRendererTests` (reuse the private `RenderAsync` helper from Task 2):

```csharp
[Fact]
public async Task RenderAsync_ResetPassword_ContainsUrl()
{
    var html = await RenderAsync(
        "~/Views/Emails/ResetPassword.cshtml",
        new EmailLinkViewModel("https://shop.test/Account/ResetPassword?code=abc"));
    Assert.Contains("https://shop.test/Account/ResetPassword?code=abc", html);
}

[Fact]
public async Task RenderAsync_OrderConfirmation_ContainsOrderAndProduct()
{
    var order = new Order
    {
        Id = 42,
        RecipientName = "Nguyen Van A",
        RecipientPhone = "0901234567",
        ShippingAddress = "12 Le Loi",
        TotalAmount = 200000,
        Method = PaymentMethod.Cod
    };
    order.Items.Add(new OrderItem { ProductName = "Áo thun", Quantity = 2, UnitPrice = 100000 });

    var html = await RenderAsync(
        "~/Views/Emails/OrderConfirmation.cshtml",
        new OrderConfirmationEmailViewModel(order, "https://shop.test/Orders/Details/42"));

    Assert.Contains("#42", html);
    Assert.Contains("Áo thun", html);
    Assert.Contains("200,000", html);
}
```

Expected: both pass.

- [ ] **Step 6: Commit**

```bash
git add ECommerce.Web/Services/Email/IEmailService.cs ECommerce.Web/Services/Email/EmailService.cs ECommerce.Web/Models/ViewModels/EmailViewModels.cs ECommerce.Web/Views/Emails/ResetPassword.cshtml ECommerce.Web/Views/Emails/OrderConfirmation.cshtml ECommerce.Web/Program.cs ECommerce.Web.Tests/Services/Email/EmailServiceTests.cs ECommerce.Web.Tests/Services/Email/RazorEmailTemplateRendererTests.cs
git commit -m "feat: add email service and password-reset/order templates"
```

---

### Task 4: Register sends confirmation; ConfirmEmail and resend actions

**Files:**
- Create: `ECommerce.Web/Models/ViewModels/ConfirmEmailViewModel.cs`
- Create: `ECommerce.Web/Models/ViewModels/ResendConfirmationViewModel.cs`
- Create: `ECommerce.Web/Views/Account/ConfirmEmail.cshtml`
- Create: `ECommerce.Web/Views/Account/ResendEmailConfirmation.cshtml`
- Modify: `ECommerce.Web/Controllers/AccountController.cs` (ctor, `Register`, new actions)
- Modify: `ECommerce.Web/Views/Account/Login.cshtml` (resend link)
- Test: `ECommerce.Web.Tests/Controllers/AccountEmailFlowTests.cs`

**Interfaces:**
- Consumes: `IdentityTokenCodec`, `IEmailService`, `IUrlHelper`, `INotyfService`.
- Produces:
  - AccountController ctor becomes `(UserManager<ApplicationUser>, SignInManager<ApplicationUser>, ICartService, IEmailService, INotyfService, ILogger<AccountController>)`.
  - `GET ConfirmEmail(string? userId, string? code)` → `ConfirmEmailViewModel(bool Success, string Message)`.
  - `GET ResendEmailConfirmation()` and `POST ResendEmailConfirmation(ResendConfirmationViewModel model)`, `ResendConfirmationViewModel.Email` is required + `[EmailAddress]`.
  - Exact copy: success `Email của bạn đã được xác thực thành công.`; failure `Liên kết xác thực không hợp lệ hoặc đã hết hạn.`; resend info `Nếu email tồn tại và chưa xác thực, chúng tôi đã gửi lại liên kết xác thực.`

- [ ] **Step 1: Write the failing controller tests**

```csharp
using System.Security.Claims;
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
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://shop.test/link");
        controller.Url = url.Object;
        return new Harness(controller, users, signIn, email);
    }

    private static ApplicationUser NewUser() => new()
    {
        Id = "u1", UserName = "buyer@test.local", Email = "buyer@test.local"
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
}
```

- [ ] **Step 2: Run and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AccountEmailFlowTests"`
Expected: compile errors (`AccountController` arity, `ConfirmEmail`, `ResendEmailConfirmation`, view models).

- [ ] **Step 3: Implement controller, view models and views**

- Ctor: add `IEmailService emailService` and `INotyfService notyf`; store both.
- `Register` (after `AddToRoleAsync`, before `SignInAsync`): generate token, encode, build
  the absolute link with `Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)`,
  send when the link is non-empty, all inside `try/catch (Exception ex)` → `LogWarning`
  and continue. Then sign in + merge cart exactly as today.
- `ConfirmEmail`: null/whitespace `userId` or `!TryDecode` → invalid view; `FindByIdAsync`
  null → invalid view; `ConfirmEmailAsync` result decides `Success` and `Message`.
- `Resend` GET returns the form; POST validates ModelState, looks up the user, sends only
  when `user != null && !await IsEmailConfirmedAsync(user)` (inside try/catch), then
  `notyf.Information(...)` and `RedirectToAction(nameof(Login))`. GET of both
  `ConfirmEmail`/`Resend` must not require authentication.
- `ConfirmEmailViewModel.cs`: `public sealed record ConfirmEmailViewModel(bool Success, string Message);`
- `ResendConfirmationViewModel.cs`: `Email` with `[Required(ErrorMessage = "Email là bắt buộc.")]`
  and `[EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]`.
- `Views/Account/ConfirmEmail.cshtml`: `@model ConfirmEmailViewModel`, card layout matching
  Login, `fa-check-circle` when success / `fa-circle-exclamation` when not, `Model.Message`,
  and a "Đăng nhập" link (`asp-action="Login"`).
- `Views/Account/ResendEmailConfirmation.cshtml`: `@model ResendConfirmationViewModel`, form
  posting to `ResendEmailConfirmation` with antiforgery, email input, submit
  "Gửi lại email xác thực", link back to Login.
- `Login.cshtml`: add a `Gửi lại email xác thực` link
  (`asp-action="ResendEmailConfirmation"`) next to the register link.

- [ ] **Step 4: Run the tests**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AccountEmailFlowTests"`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add ECommerce.Web/Controllers/AccountController.cs ECommerce.Web/Models/ViewModels/ConfirmEmailViewModel.cs ECommerce.Web/Models/ViewModels/ResendConfirmationViewModel.cs ECommerce.Web/Views/Account/ConfirmEmail.cshtml ECommerce.Web/Views/Account/ResendEmailConfirmation.cshtml ECommerce.Web/Views/Account/Login.cshtml ECommerce.Web.Tests/Controllers/AccountEmailFlowTests.cs
git commit -m "feat: send and confirm verification email from registration"
```

---

### Task 5: Forgot and reset password

**Files:**
- Create: `ECommerce.Web/Models/ViewModels/ForgotPasswordViewModel.cs`
- Create: `ECommerce.Web/Models/ViewModels/ResetPasswordViewModel.cs`
- Create: `ECommerce.Web/Views/Account/ForgotPassword.cshtml`
- Create: `ECommerce.Web/Views/Account/ForgotPasswordConfirmation.cshtml`
- Create: `ECommerce.Web/Views/Account/ResetPassword.cshtml`
- Create: `ECommerce.Web/Views/Account/ResetPasswordConfirmation.cshtml`
- Modify: `ECommerce.Web/Controllers/AccountController.cs`
- Modify: `ECommerce.Web/Views/Account/Login.cshtml` (forgot-password link)
- Test: `ECommerce.Web.Tests/Controllers/AccountEmailFlowTests.cs` (add cases)

**Interfaces:**
- Consumes: Task 4's `AccountController` dependencies + `IdentityTokenCodec`.
- Produces:
  - `GET ForgotPassword()`, `POST ForgotPassword(ForgotPasswordViewModel model)`,
    `GET ForgotPasswordConfirmation()`.
  - `GET ResetPassword(string? userId, string? code)`,
    `POST ResetPassword(ResetPasswordViewModel model)`, `GET ResetPasswordConfirmation()`.
  - `ForgotPasswordViewModel { string Email }`, `ResetPasswordViewModel { string UserId; string Code; string Password; string ConfirmPassword }` (Password rules mirror `RegisterViewModel`).
  - Exact errors: reset invalid `Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.`; success page `Mật khẩu của bạn đã được đặt lại. Vui lòng đăng nhập bằng mật khẩu mới.`

- [ ] **Step 1: Write the failing tests (append to `AccountEmailFlowTests`)**

```csharp
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
```

- [ ] **Step 2: Run and verify it fails**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AccountEmailFlowTests"`
Expected: compile errors for the new actions/view models.

- [ ] **Step 3: Implement actions, view models and views**

- `ForgotPassword` POST: ModelState invalid → view; `FindByEmailAsync`; when found,
  generate + encode + `Url.Action(nameof(ResetPassword), "Account", new { userId, code }, Request.Scheme)`
  and `SendPasswordResetAsync` inside try/catch (log warning). Always
  `RedirectToAction(nameof(ForgotPasswordConfirmation))`.
- `ResetPassword` GET: missing `userId` or blank `code` → `RedirectToAction(nameof(ForgotPassword))`;
  otherwise `View(new ResetPasswordViewModel { UserId = userId, Code = code })`.
- `ResetPassword` POST: ModelState invalid → view; `TryDecode(model.Code, out var token)` false
  → add the exact invalid-link error to `ModelState` via `ModelState.AddModelError(string.Empty, ...)`
  and return view; `FindByIdAsync` null → same error; `ResetPasswordAsync(user, token, model.Password)`;
  success → `RedirectToAction(nameof(ResetPasswordConfirmation))`, failure → add each
  `IdentityError.Description` under `string.Empty` and return view.
- The four views mirror the Login card styling: ForgotPassword (email input, submit
  "Gửi liên kết đặt lại"), ForgotPasswordConfirmation (static success text "Nếu email tồn
  tại, chúng tôi đã gửi liên kết đặt lại mật khẩu. Vui lòng kiểm tra hộp thư."), ResetPassword
  (hidden `UserId` + `Code` via `asp-for`, password + confirm inputs, submit
  "Đặt lại mật khẩu"), ResetPasswordConfirmation (success text from Produces + Login link).
  All forms: `method="post"`, antiforgery, `asp-validation-summary="ModelOnly"`.
- `Login.cshtml`: add `<a asp-action="ForgotPassword" ...>Quên mật khẩu?</a>` at the end of
  the `.flex.items-center.justify-between` row.

- [ ] **Step 4: Run the controller tests**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AccountEmailFlowTests"`
Expected: 12 passed.

- [ ] **Step 5: Commit**

```bash
git add ECommerce.Web/Controllers/AccountController.cs ECommerce.Web/Models/ViewModels/ForgotPasswordViewModel.cs ECommerce.Web/Models/ViewModels/ResetPasswordViewModel.cs ECommerce.Web/Views/Account/ForgotPassword.cshtml ECommerce.Web/Views/Account/ForgotPasswordConfirmation.cshtml ECommerce.Web/Views/Account/ResetPassword.cshtml ECommerce.Web/Views/Account/ResetPasswordConfirmation.cshtml ECommerce.Web.Tests/Controllers/AccountEmailFlowTests.cs
git commit -m "feat: add forgot and reset password flows"
```

---

### Task 6: Order confirmation on checkout

**Files:**
- Modify: `ECommerce.Web/Controllers/OrdersController.cs` (ctor + `Checkout`)
- Modify: `ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs` (helper + new tests)

**Interfaces:**
- Consumes: `IEmailService.SendOrderConfirmationAsync(int orderId, string orderUrl)`.
- Produces: `OrdersController` ctor gains `IEmailService emailService` as the parameter
  immediately before `ILogger<OrdersController>`.

- [ ] **Step 1: Update the test helper and add the failing tests**

Change `CreateController` to accept `Mock<IEmailService>? emailService = null` as the
**last** parameter (after the existing `string userId = "user-checkout"`), pass
`(emailService ?? new Mock<IEmailService>()).Object` to the controller, and set a URL
helper on every controller it builds:

```csharp
var url = new Mock<IUrlHelper>();
url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://shop.test/Orders/Details/1");
controller.Url = url.Object;
```

(Add `using ECommerce.Web.Services.Email;`, `using Microsoft.AspNetCore.Mvc.Routing;`.)

Add:

```csharp
[Fact]
public async Task Checkout_WhenOrderCreated_SendsOrderConfirmationEmailOnce()
{
    var orderService = new Mock<IOrderService>();
    orderService.Setup(s => s.CreateOrderFromCartAsync(
            It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>()))
        .ReturnsAsync((true, "ok", 77));
    var paymentService = new Mock<IPaymentService>();
    paymentService.Setup(s => s.StartPaymentAsync(
            It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
        .ReturnsAsync(new PaymentInitResult { Success = true });
    var email = new Mock<IEmailService>();

    var controller = CreateController(orderService, new Mock<ICartService>(), paymentService, emailService: email);
    var model = new CheckoutViewModel
    {
        RecipientName = "A", RecipientPhone = "0901234567", ShippingAddress = "12 Le Loi",
        PaymentMethod = PaymentMethod.VnPay, Cart = new CartViewModel()
    };

    await controller.Checkout(model);

    email.Verify(e => e.SendOrderConfirmationAsync(77, "https://shop.test/Orders/Details/1"), Times.Once);
}

[Fact]
public async Task Checkout_WhenOrderEmailThrows_StillRedirectsToOrderDetails()
{
    var orderService = new Mock<IOrderService>();
    orderService.Setup(s => s.CreateOrderFromCartAsync(
            It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>()))
        .ReturnsAsync((true, "ok", 88));
    var paymentService = new Mock<IPaymentService>();
    paymentService.Setup(s => s.StartPaymentAsync(
            It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
        .ReturnsAsync(new PaymentInitResult { Success = true });
    var email = new Mock<IEmailService>();
    email.Setup(e => e.SendOrderConfirmationAsync(It.IsAny<int>(), It.IsAny<string>()))
        .ThrowsAsync(new InvalidOperationException("smtp down"));

    var controller = CreateController(orderService, new Mock<ICartService>(), paymentService, emailService: email);
    var model = new CheckoutViewModel
    {
        RecipientName = "A", RecipientPhone = "0901234567", ShippingAddress = "12 Le Loi",
        PaymentMethod = PaymentMethod.Cod, Cart = new CartViewModel()
    };

    var result = await controller.Checkout(model);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
    Assert.Equal(88, redirect.RouteValues?["id"]);
}
```

- [ ] **Step 2: Run and verify they fail**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~CheckoutControllerTests"`
Expected: compile error on the extra constructor argument, then (once wired) the new
assertions fail because no email is sent.

- [ ] **Step 3: Wire the email into checkout**

Add `IEmailService` to the ctor (field + assignment), then in `Checkout` insert
immediately after the `if (!success || !orderId.HasValue)` guard and **before**
`PaymentInitResult payment;`:

```csharp
var orderUrl = Url.Action(nameof(Details), "Orders", new { id = orderId.Value }, Request.Scheme)
    ?? string.Empty;
try
{
    await _emailService.SendOrderConfirmationAsync(orderId.Value, orderUrl);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Không gửi được email xác nhận cho đơn hàng {OrderId}.", orderId.Value);
}
```

- [ ] **Step 4: Run the whole checkout test class**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~CheckoutControllerTests"`
Expected: all pass (6 tests).

- [ ] **Step 5: Commit**

```bash
git add ECommerce.Web/Controllers/OrdersController.cs ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs
git commit -m "feat: email order confirmation after checkout without failing the request"
```

---

### Task 7: Full sweep and live verification

**Files:**
- Modify: `C:\Users\hieub\AppData\Local\Temp\opencode\live-verify.ps1` (reuse the existing
  boot/drive/teardown scaffolding from the Phase A script) — not committed.

- [ ] **Step 1: Build and run the full suite**

Run: `dotnet build ECommerce.Web.slnx --no-incremental`
Expected: `0 Warning(s)`.

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj`
Expected: all tests pass (130 existing + the new ones).

- [ ] **Step 2: Add the email checks to the live script (one boot, then teardown)**

Checks to implement, each printed as `CHECK <name> = True/False`:

1. `register_new_user` — POST `/Account/Register` with a unique address
   `b1.<timestamp>@technova.local`, password `Qa!23456x`; expect 302.
2. `confirm_link_logged` — the dev log contains `/Account/ConfirmEmail?`; extract the URL,
   `[System.Net.WebUtility]::HtmlDecode` it (the HTML escapes `&` as `&amp;`).
3. `confirm_success_page` — GET that URL; body contains `Email của bạn đã được xác thực thành công.`
4. `login_after_confirm` — `POST /Account/Logout` first, then POST `/Account/Login`;
   expect 302 (no error text on the page).
5. `forgot_unknown_no_email` — POST `/Account/ForgotPassword` with
   `nobody.<timestamp>@technova.local`; expect 302 to `/Account/ForgotPasswordConfirmation`
   and **no** new `=== EMAIL (dev) ===` block in the log.
6. `forgot_known_sends_link` — POST with the registered address; expect 302 and a new log
   block whose body contains `/Account/ResetPassword?`.
7. `reset_password` — extract that URL, GET it (form renders), POST it with the hidden
   `UserId`/`Code` + `NewQa!23456`; expect 302 to `/Account/ResetPasswordConfirmation`.
8. `login_with_new_password` — `POST /Account/Logout` first, then POST `/Account/Login`
   with the new password; expect 302.
9. `order_email_sent` — log in, add a product to the cart, POST checkout (COD, valid
   shipping); expect 302 to `/Orders/Details/{id}`, and the log contains
   `Xác nhận đơn hàng #{id} – TechNova` plus the product name used in the order.
10. `teardown_clean` — 0 listeners on port 5080 and 0 `ECommerce.Web` processes remain.

- [ ] **Step 3: Run the script**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Users\hieub\AppData\Local\Temp\opencode\live-verify.ps1`
Expected: every check `True`, `FAILED_CHECKS=0`.

- [ ] **Step 4: Report**

Record in the SDD ledger/report: the commit chain for B1, test count delta, the live-check
result, and the side effects on the dev database (new user `b1.<timestamp>@technova.local`
and one COD order created by the probe).
