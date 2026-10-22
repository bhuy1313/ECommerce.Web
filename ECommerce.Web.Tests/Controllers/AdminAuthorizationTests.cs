using System.Reflection;
using ECommerce.Web.Controllers;
using ECommerce.Web.Models.Constants;
using Microsoft.AspNetCore.Authorization;

namespace ECommerce.Web.Tests.Controllers;

/// <summary>
/// Khóa ma trận phân quyền AdminController: action nào Operator được chạm,
/// action nào Admin-only. Chạy bằng reflection trên attribute, không cần host.
/// </summary>
public class AdminAuthorizationTests
{
    private static readonly string[] AdminOnlyActions =
    [
        nameof(AdminController.Index),
        nameof(AdminController.Users),
        nameof(AdminController.EditUserRoles),
        nameof(AdminController.UpdatePaymentStatus),
        nameof(AdminController.Coupons),
        nameof(AdminController.CreateCoupon),
        nameof(AdminController.EditCoupon),
        nameof(AdminController.ToggleCouponStatus),
        nameof(AdminController.DeleteCoupon),
        nameof(AdminController.Reviews),
        nameof(AdminController.ApproveReview),
        nameof(AdminController.DeleteReview),
    ];

    private static readonly string[] OperatorAllowedActions =
    [
        nameof(AdminController.Categories),
        nameof(AdminController.CreateCategory),
        nameof(AdminController.EditCategory),
        nameof(AdminController.ToggleCategoryStatus),
        nameof(AdminController.Products),
        nameof(AdminController.CreateProduct),
        nameof(AdminController.EditProduct),
        nameof(AdminController.ToggleProductStatus),
        nameof(AdminController.Orders),
        nameof(AdminController.OrderDetails),
        nameof(AdminController.UpdateOrderStatus),
    ];

    [Fact]
    public void AdminController_ClassLevel_AllowsAdminAndOperator()
    {
        var classAuth = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(classAuth);
        Assert.Equal(AppRoles.AdminOrOperator, classAuth.Roles);
    }

    [Theory]
    [InlineData(nameof(AdminController.Index))]
    [InlineData(nameof(AdminController.Users))]
    [InlineData(nameof(AdminController.EditUserRoles))]
    [InlineData(nameof(AdminController.UpdatePaymentStatus))]
    [InlineData(nameof(AdminController.Coupons))]
    [InlineData(nameof(AdminController.CreateCoupon))]
    [InlineData(nameof(AdminController.EditCoupon))]
    [InlineData(nameof(AdminController.ToggleCouponStatus))]
    [InlineData(nameof(AdminController.DeleteCoupon))]
    [InlineData(nameof(AdminController.Reviews))]
    [InlineData(nameof(AdminController.ApproveReview))]
    [InlineData(nameof(AdminController.DeleteReview))]
    public void AdminOnlyActions_RequireAdminRole(string actionName)
    {
        var methods = typeof(AdminController).GetMethods()
            .Where(m => m.Name == actionName && m.DeclaringType == typeof(AdminController));

        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            var methodAuth = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(methodAuth);
            Assert.Equal(AppRoles.Admin, methodAuth.Roles);
        }
    }

    [Theory]
    [InlineData(nameof(AdminController.Categories))]
    [InlineData(nameof(AdminController.CreateCategory))]
    [InlineData(nameof(AdminController.EditCategory))]
    [InlineData(nameof(AdminController.ToggleCategoryStatus))]
    [InlineData(nameof(AdminController.Products))]
    [InlineData(nameof(AdminController.CreateProduct))]
    [InlineData(nameof(AdminController.EditProduct))]
    [InlineData(nameof(AdminController.ToggleProductStatus))]
    [InlineData(nameof(AdminController.Orders))]
    [InlineData(nameof(AdminController.OrderDetails))]
    [InlineData(nameof(AdminController.UpdateOrderStatus))]
    public void OperatorAllowedActions_AreNotRestrictedToAdminOnly(string actionName)
    {
        var methods = typeof(AdminController).GetMethods()
            .Where(m => m.Name == actionName && m.DeclaringType == typeof(AdminController));

        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            // Không siết Admin-only ở method-level => thừa hưởng AdminOrOperator từ class.
            var methodAuth = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.True(methodAuth is null || methodAuth.Roles != AppRoles.Admin);
        }
    }

    [Fact]
    public void EveryAdminAction_IsCoveredByAuthorizationMatrix()
    {
        var actionNames = typeof(AdminController).GetMethods()
            .Where(m => m.DeclaringType == typeof(AdminController)
                && m.IsPublic
                && !m.IsSpecialName
                && m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute))
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var covered = AdminOnlyActions.Concat(OperatorAllowedActions).Distinct().OrderBy(n => n).ToList();

        Assert.Equal(covered, actionNames);
    }
}
