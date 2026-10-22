using System.Reflection;
using ECommerce.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Tests.Controllers;

/// <summary>
/// Khóa policy rate limit cho các thao tác ghi của AdminController.
/// Admin-only + CSRF là chưa đủ để chống spam/abuse từ một phiên admin bị lộ.
/// </summary>
public class AdminRateLimitingTests
{
    [Fact]
    public void AdminController_PostActions_UseMutatingRateLimit()
    {
        var postActions = typeof(AdminController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToArray();

        Assert.NotEmpty(postActions);

        var missing = postActions
            .Where(m => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName != "mutating")
            .Select(m => m.Name)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Thiếu [EnableRateLimiting(\"mutating\")]: " + string.Join(", ", missing));
    }
}
