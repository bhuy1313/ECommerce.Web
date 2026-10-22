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

public class ProductsControllerCatalogRatingTests
{
    [Fact]
    public async Task Index_LoadsRatingSummariesForListedProducts()
    {
        var (controller, db, connection, reviews) = CreateController();
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.GetSummariesAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, ReviewSummary>
            {
                [10] = new ReviewSummary(4.5m, 3)
            });

        var result = await controller.Index(null, null);

        var model = Assert.IsType<ProductListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(4.5m, model.Ratings[10].AverageRating);
        Assert.Equal(3, model.Ratings[10].Count);
        reviews.Verify(s => s.GetSummariesAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(10))), Times.Once);
    }

    [Fact]
    public async Task Index_WhenNoReviews_LeavesRatingsEmpty()
    {
        var (controller, db, connection, reviews) = CreateController();
        using var _c = connection;
        using var _d = db;
        reviews.Setup(s => s.GetSummariesAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, ReviewSummary>());

        var result = await controller.Index(null, null);

        var model = Assert.IsType<ProductListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Ratings);
    }

    private static (ProductsController Controller, ApplicationDbContext Db, SqliteConnection Connection, Mock<IReviewService> Reviews)
        CreateController()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.Categories.Add(new Category { Id = 1, Name = "Laptop", IsActive = true });
        db.Products.Add(new Product { Id = 10, Name = "Laptop A", Price = 1000m, StockQuantity = 5, CategoryId = 1, IsActive = true });
        db.SaveChanges();

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns((string?)null);

        var reviews = new Mock<IReviewService>();
        var controller = new ProductsController(db, reviews.Object, userManager.Object, new Mock<INotyfService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, db, connection, reviews);
    }
}
