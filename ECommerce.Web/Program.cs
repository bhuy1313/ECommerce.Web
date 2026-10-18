using ECommerce.Web.Data;
using ECommerce.Web.Middlewares;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
using ECommerce.Web.Services.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddNotyf(config =>
{
    config.DurationInSeconds = 5;
    config.IsDismissable = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Currency:ApiBaseUrl"] ?? CurrencyService.DefaultApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("VnPay"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<VnPayOptions>>().Value);
builder.Services.AddSingleton<IPaymentGateway, CodPaymentGateway>();
builder.Services.AddScoped<IPaymentGateway, VnPayGateway>();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var useSqlite = defaultConnection.Contains(".db", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(defaultConnection);
    }
    else
    {
        options.UseSqlServer(defaultConnection);
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // SameAsRequest chỉ cho dev http://localhost; production luôn ép Secure.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Chống brute-force Login/Register: mặc định 10 req/phút theo IP.
// E2E (Playwright) chạy dày từ cùng 1 IP nên cho phép nâng qua cấu hình
// "RateLimiting:AuthPermitLimit" (env: RateLimiting__AuthPermitLimit).
builder.Services.AddRateLimiter(options =>
{
    var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 10;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    // Chống spam/abuse các thao tác thay đổi dữ liệu: 60 req/phút theo IP.
    options.AddPolicy("mutating", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Quá nhiều yêu cầu. Vui lòng thử lại sau.", cancellationToken);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    if (useSqlite)
    {
        // Dùng MigrateAsync cho SQLite (prod hardening): tránh bỏ qua migration incremental
        await db.Database.MigrateAsync();
        // Demo nhiều người bấm cùng lúc: WAL cho phép đọc song song khi đang ghi,
        // giảm lỗi "database is locked" (vẫn có thể xảy ra nếu 2 checkout cùng lúc —
        // xem runbook demo).
        try
        {
            var journalMode = (await db.Database.SqlQueryRaw<string>("PRAGMA journal_mode=WAL;").ToListAsync())
                .FirstOrDefault();
            var sqliteLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SQLite");
            sqliteLogger.LogInformation("SQLite journal_mode={JournalMode}", journalMode);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SQLite");
            logger.LogWarning(ex, "Failed to set/read PRAGMA journal_mode=WAL");
        }
    }
    else
    {
        await db.Database.MigrateAsync();
    }
    await DbInitializer.SeedAsync(
        db,
        userManager,
        roleManager,
        services.GetRequiredService<IConfiguration>(),
        services.GetRequiredService<IHostEnvironment>(),
        services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
}

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandling();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

// Health check của host gọi qua HTTP nội bộ — không redirect sang HTTPS
// (một số checker không theo redirect, service sẽ mãi không healthy).
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/healthz"),
    branch => branch.UseHttpsRedirection());
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseNotyf();

app.MapStaticAssets();

app.MapGet("/healthz", () => Results.Ok("healthy"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
