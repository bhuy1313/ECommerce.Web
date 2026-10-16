using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Web.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var requestId = Activity.Current?.Id ?? context.TraceIdentifier;
            var path = context.Request.Path;
            var method = context.Request.Method;
            var user = context.User.Identity?.Name ?? "Anonymous";

            _logger.LogError(ex,
                "[GlobalExceptionMiddleware] Ngoại lệ chưa xử lý: {Method} {Path} bởi người dùng '{User}'. RequestId: {RequestId}",
                method, path, user, requestId);

            if (IsApiOrAjaxRequest(context.Request))
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Đã xảy ra lỗi máy chủ nội bộ",
                    Detail = "Sự cố kỹ thuật không mong muốn. Vui lòng liên hệ hỗ trợ kèm mã RequestId.",
                    Instance = path,
                    Extensions = { ["requestId"] = requestId }
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
            }
            else
            {
                // Đối với Web UI (HTML), rethrow để ASP.NET Core UseExceptionHandler("/Home/Error") hiển thị View thân thiện
                throw;
            }
        }
    }

    private static bool IsApiOrAjaxRequest(HttpRequest request)
    {
        if (request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
            string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
