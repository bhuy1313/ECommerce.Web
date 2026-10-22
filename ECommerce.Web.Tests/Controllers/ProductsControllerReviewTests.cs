using System.Security.Claims;
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Controllers;
using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Moq;

namespace ECommerce.Web.Tests.Controllers;

public class ProductsControllerReviewTests
{
    private const int ProductId = 10;
    private const string UserId = "user-1";

    [Fact]
    public async Task Details_WhenEligibleUser_ShowsReviewForm()
    {
        var (controller, db, connection, reviews, _) = CreateController(UserId);
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.GetApprovedAsync(ProductId)).ReturnsAsync(Array.Empty<Review>());
        reviews.Setup(s => s.GetSummaryAsync(ProductId)).ReturnsAsync(new ReviewSummary(0m, 0));
        reviews.Setup(s => s.HasReviewedAsync(UserId, ProductId)).ReturnsAsync(false);
        reviews.Setup(s => s.CanReviewAsync(UserId, ProductId)).ReturnsAsync(true);

        var result = await controller.Details(ProductId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProductDetailsViewModel>(view.Model);
        Assert.True(model.CanReview);
        Assert.False(model.HasReviewed);
        Assert.Equal(ProductId, model.Product.Id);
    }

    [Fact]
    public async Task Details_WhenAnonymous_HidesReviewForm()
    {
        var (controller, db, connection, reviews, _) = CreateController(null);
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.GetApprovedAsync(ProductId)).ReturnsAsync(Array.Empty<Review>());
        reviews.Setup(s => s.GetSummaryAsync(ProductId)).ReturnsAsync(new ReviewSummary(0m, 0));

        var result = await controller.Details(ProductId);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProductDetailsViewModel>(view.Model);
        Assert.False(model.CanReview);
        Assert.False(model.HasReviewed);
        reviews.Verify(s => s.CanReviewAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Details_ShowsApprovedReviewsAndSummary()
    {
        var (controller, db, connection, reviews, _) = CreateController(null);
        using var _c = connection;
        using var _d = db;
        var approved = new List<Review>
        {
            new() { Id = 1, ProductId = ProductId, UserId = UserId, Rating = 5, Comment = "Tốt", IsApproved = true }
        };
        reviews.Setup(s => s.GetApprovedAsync(ProductId)).ReturnsAsync(approved);
        reviews.Setup(s => s.GetSummaryAsync(ProductId)).ReturnsAsync(new ReviewSummary(4.5m, 2));

        var result = await controller.Details(ProductId);

        var model = Assert.IsType<ProductDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Single(model.Reviews);
        Assert.Equal(4.5m, model.AverageRating);
        Assert.Equal(2, model.ReviewCount);
    }

    [Fact]
    public async Task AddReview_WhenValid_RedirectsAndToastsSuccess()
    {
        var (controller, db, connection, reviews, notyf) = CreateController(UserId);
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.CreateAsync(UserId, ProductId, 5, "Rất tốt"))
            .ReturnsAsync((true, "Cảm ơn bạn! Đánh giá đang chờ kiểm duyệt."));

        var result = await controller.AddReview(ProductId, new ReviewFormViewModel { Rating = 5, Comment = "Rất tốt" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Details), redirect.ActionName);
        Assert.Equal(ProductId, redirect.RouteValues!["id"]);
        notyf.Verify(n => n.Success("Cảm ơn bạn! Đánh giá đang chờ kiểm duyệt."), Times.Once);
    }

    [Fact]
    public async Task AddReview_WhenModelInvalid_ToastsErrorAndDoesNotCallService()
    {
        var (controller, db, connection, reviews, notyf) = CreateController(UserId);
        using var _c = connection;
        using var _d = db;
        controller.ModelState.AddModelError("Rating", "Vui lòng chọn từ 1 đến 5 sao.");

        var result = await controller.AddReview(ProductId, new ReviewFormViewModel { Rating = 0 });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Details), redirect.ActionName);
        notyf.Verify(n => n.Error("Vui lòng chọn từ 1 đến 5 sao."), Times.Once);
        reviews.Verify(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddReview_WhenServiceRejects_ToastsError()
    {
        var (controller, db, connection, reviews, notyf) = CreateController(UserId);
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.CreateAsync(UserId, ProductId, 4, "Hay"))
            .ReturnsAsync((false, "Bạn đã đánh giá sản phẩm này."));

        var result = await controller.AddReview(ProductId, new ReviewFormViewModel { Rating = 4, Comment = "Hay" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProductsController.Details), redirect.ActionName);
        notyf.Verify(n => n.Error("Bạn đã đánh giá sản phẩm này."), Times.Once);
    }

    private static (ProductsController Controller, ApplicationDbContext Db, SqliteConnection Connection, Mock<IReviewService> Reviews, Mock<INotyfService> Notyf)
        CreateController(string? userId)
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.Categories.Add(new Category { Id = 1, Name = "Laptop", IsActive = true });
        db.Products.Add(new Product
        {
            Id = ProductId,
            Name = "Laptop A",
            Price = 1000m,
            StockQuantity = 5,
            CategoryId = 1,
            IsActive = true
        });
        db.SaveChanges();

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId!);

        var reviews = new Mock<IReviewService>();
        var notyf = new Mock<INotyfService>();
        var controller = new ProductsController(db, reviews.Object, userManager.Object, notyf.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        return (controller, db, connection, reviews, notyf);
    }
}
