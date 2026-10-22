using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;

namespace ECommerce.Web.Tests.Services;

public class ReviewServiceTests
{
    private const string UserId = "user-1";
    private const int ProductId = 10;
    private const int OtherProductId = 11;

    [Fact]
    public async Task CanReview_WhenCompletedOrderContainsProduct_ReturnsTrue()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        var service = new ReviewService(db);

        var result = await service.CanReviewAsync(UserId, ProductId);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CanReview_WhenOrderNotCompleted_ReturnsFalse(OrderStatus status)
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, status, ProductId);
        var service = new ReviewService(db);

        var result = await service.CanReviewAsync(UserId, ProductId);

        Assert.False(result);
    }

    [Fact]
    public async Task CanReview_WhenCompletedOrderHasDifferentProduct_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, OtherProductId);
        var service = new ReviewService(db);

        var result = await service.CanReviewAsync(UserId, ProductId);

        Assert.False(result);
    }

    [Fact]
    public async Task CanReview_WhenAlreadyReviewed_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        db.Reviews.Add(NewReview(UserId, ProductId, 5));
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var result = await service.CanReviewAsync(UserId, ProductId);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CanReview_WhenUserMissing_ReturnsFalse(string userId)
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var service = new ReviewService(db);

        var result = await service.CanReviewAsync(userId, ProductId);

        Assert.False(result);
    }

    [Fact]
    public async Task Create_WhenEligible_AddsUnapprovedReview()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        var service = new ReviewService(db);

        var (success, message) = await service.CreateAsync(UserId, ProductId, 5, "  Rất tốt  ");

        Assert.True(success);
        Assert.Equal("Cảm ơn bạn! Đánh giá đang chờ kiểm duyệt.", message);
        var review = Assert.Single(db.Reviews);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Rất tốt", review.Comment);
        Assert.False(review.IsApproved);
        Assert.Equal(UserId, review.UserId);
        Assert.Equal(ProductId, review.ProductId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Create_WhenRatingOutOfRange_ReturnsFalse(int rating)
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        var service = new ReviewService(db);

        var (success, message) = await service.CreateAsync(UserId, ProductId, rating, "Nội dung");

        Assert.False(success);
        Assert.Equal("Điểm đánh giá phải từ 1 đến 5 sao.", message);
        Assert.Empty(db.Reviews);
    }

    [Fact]
    public async Task Create_WhenCommentTooLong_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        var service = new ReviewService(db);

        var (success, message) = await service.CreateAsync(UserId, ProductId, 4, new string('a', 1001));

        Assert.False(success);
        Assert.Equal("Nội dung đánh giá tối đa 1000 ký tự.", message);
        Assert.Empty(db.Reviews);
    }

    [Fact]
    public async Task Create_WhenAlreadyReviewed_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        db.Reviews.Add(NewReview(UserId, ProductId, 5));
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var (success, message) = await service.CreateAsync(UserId, ProductId, 3, "Lần hai");

        Assert.False(success);
        Assert.Equal("Bạn đã đánh giá sản phẩm này.", message);
        Assert.Single(db.Reviews);
    }

    [Fact]
    public async Task Create_WhenNotPurchased_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var service = new ReviewService(db);

        var (success, message) = await service.CreateAsync(UserId, ProductId, 5, "Chưa mua");

        Assert.False(success);
        Assert.Equal("Bạn chỉ có thể đánh giá sản phẩm đã mua và đơn hàng đã hoàn tất.", message);
        Assert.Empty(db.Reviews);
    }

    [Fact]
    public async Task Create_WhenCommentBlank_StoresNull()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        await AddOrderAsync(db, UserId, OrderStatus.Completed, ProductId);
        var service = new ReviewService(db);

        var (success, _) = await service.CreateAsync(UserId, ProductId, 4, "   ");

        Assert.True(success);
        Assert.Null(Assert.Single(db.Reviews).Comment);
    }

    [Fact]
    public async Task GetApproved_ReturnsOnlyApprovedNewestFirst()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var older = NewReview(UserId, ProductId, 3, approved: true, createdAt: DateTime.UtcNow.AddDays(-2));
        var newer = NewReview("user-2", ProductId, 5, approved: true, createdAt: DateTime.UtcNow);
        var pending = NewReview("user-3", ProductId, 1, approved: false);
        await AddUserAsync(db, "user-2", "second@test.local");
        await AddUserAsync(db, "user-3", "third@test.local");
        db.Reviews.AddRange(older, newer, pending);
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var reviews = await service.GetApprovedAsync(ProductId);

        Assert.Equal(2, reviews.Count);
        Assert.Equal(newer.Id, reviews[0].Id);
        Assert.Equal(older.Id, reviews[1].Id);
    }

    [Fact]
    public async Task GetSummary_ComputesAverageOfApprovedOnly()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        db.Reviews.AddRange(
            NewReview(UserId, ProductId, 1, approved: true),
            NewReview("user-2", ProductId, 1, approved: true),
            NewReview("user-3", ProductId, 2, approved: true),
            NewReview("user-4", ProductId, 5, approved: false));
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var summary = await service.GetSummaryAsync(ProductId);

        Assert.Equal(1.3m, summary.AverageRating);
        Assert.Equal(3, summary.Count);
    }

    [Fact]
    public async Task GetSummary_WhenNoReviews_ReturnsZero()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var service = new ReviewService(db);

        var summary = await service.GetSummaryAsync(ProductId);

        Assert.Equal(0m, summary.AverageRating);
        Assert.Equal(0, summary.Count);
    }

    [Fact]
    public async Task GetSummaries_GroupsByProduct()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        db.Reviews.AddRange(
            NewReview(UserId, ProductId, 5, approved: true),
            NewReview("user-2", ProductId, 4, approved: true),
            NewReview("user-3", ProductId, 1, approved: false),
            NewReview("user-4", OtherProductId, 3, approved: true));
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var summaries = await service.GetSummariesAsync(new[] { ProductId, OtherProductId });

        Assert.Equal(4.5m, summaries[ProductId].AverageRating);
        Assert.Equal(2, summaries[ProductId].Count);
        Assert.Equal(3m, summaries[OtherProductId].AverageRating);
        Assert.Equal(1, summaries[OtherProductId].Count);
    }

    [Fact]
    public async Task Approve_SetsIsApproved()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var review = NewReview(UserId, ProductId, 5, approved: false);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var (success, message) = await service.ApproveAsync(review.Id);

        Assert.True(success);
        Assert.Equal("Đã duyệt đánh giá.", message);
        Assert.True(db.Reviews.Single().IsApproved);
    }

    [Fact]
    public async Task Approve_WhenMissing_ReturnsFalse()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var service = new ReviewService(db);

        var (success, message) = await service.ApproveAsync(999);

        Assert.False(success);
        Assert.Equal("Không tìm thấy đánh giá.", message);
    }

    [Fact]
    public async Task Delete_RemovesReview()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var review = NewReview(UserId, ProductId, 5);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var (success, message) = await service.DeleteAsync(review.Id);

        Assert.True(success);
        Assert.Equal("Đã xóa đánh giá.", message);
        Assert.Empty(db.Reviews);
    }

    [Fact]
    public async Task GetAll_WhenPendingFilter_ReturnsUnapproved()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        db.Reviews.AddRange(
            NewReview(UserId, ProductId, 5, approved: true),
            NewReview("user-2", ProductId, 4, approved: false));
        await AddUserAsync(db, "user-2", "second@test.local");
        await db.SaveChangesAsync();
        var service = new ReviewService(db);

        var reviews = await service.GetAllAsync(approved: false);

        var review = Assert.Single(reviews);
        Assert.False(review.IsApproved);
        Assert.NotNull(review.Product);
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();

        db.Users.Add(new ApplicationUser { Id = UserId, UserName = "buyer@test.local", Email = "buyer@test.local" });
        db.Categories.Add(new Category { Id = 1, Name = "Laptop", IsActive = true });        db.Products.Add(new Product { Id = ProductId, Name = "Laptop A", Price = 1000m, StockQuantity = 5, CategoryId = 1 });
        db.Products.Add(new Product { Id = OtherProductId, Name = "Laptop B", Price = 2000m, StockQuantity = 5, CategoryId = 1 });
        await db.SaveChangesAsync();

        return (db, connection);
    }

    private static async Task AddOrderAsync(ApplicationDbContext db, string userId, OrderStatus status, int productId)
    {
        var order = new Order
        {
            UserId = userId,
            RecipientName = "Người nhận",
            RecipientPhone = "0901234567",
            ShippingAddress = "Hà Nội",
            Status = status,
            TotalAmount = 1000m
        };
        order.Items.Add(new OrderItem
        {
            ProductId = productId,
            ProductName = "Laptop",
            Quantity = 1,
            UnitPrice = 1000m
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }

    private static async Task AddUserAsync(ApplicationDbContext db, string userId, string email)
    {
        db.Users.Add(new ApplicationUser { Id = userId, UserName = email, Email = email });
        await db.SaveChangesAsync();
    }

    private static Review NewReview(
        string userId,
        int productId,
        int rating,
        bool approved = false,
        DateTime? createdAt = null) => new()
        {
            UserId = userId,
            ProductId = productId,
            Rating = rating,
            Comment = "Đánh giá",
            IsApproved = approved,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
}
