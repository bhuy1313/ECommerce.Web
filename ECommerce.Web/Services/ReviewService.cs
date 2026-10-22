using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services;

public class ReviewService : IReviewService
{
    private const string DuplicateMessage = "Bạn đã đánh giá sản phẩm này.";
    private const string NotFoundMessage = "Không tìm thấy đánh giá.";
    private const string NotPurchasedMessage = "Bạn chỉ có thể đánh giá sản phẩm đã mua và đơn hàng đã hoàn tất.";
    private const int MaxCommentLength = 1000;

    private readonly ApplicationDbContext _db;

    public ReviewService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanReviewAsync(string userId, int productId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (await HasReviewedAsync(userId, productId))
        {
            return false;
        }

        return await HasCompletedOrderAsync(userId, productId);
    }

    public Task<bool> HasReviewedAsync(string userId, int productId)
    {
        return _db.Reviews.AnyAsync(r => r.UserId == userId && r.ProductId == productId);
    }

    public async Task<(bool Success, string Message)> CreateAsync(string userId, int productId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
        {
            return (false, "Điểm đánh giá phải từ 1 đến 5 sao.");
        }

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed is not null && trimmed.Length > MaxCommentLength)
        {
            return (false, "Nội dung đánh giá tối đa 1000 ký tự.");
        }

        if (await HasReviewedAsync(userId, productId))
        {
            return (false, DuplicateMessage);
        }

        if (!await HasCompletedOrderAsync(userId, productId))
        {
            return (false, NotPurchasedMessage);
        }

        var review = new Review
        {
            UserId = userId,
            ProductId = productId,
            Rating = rating,
            Comment = trimmed,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Reviews.Add(review);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index (ProductId, UserId) là chốt chặn race giữa 2 request đồng thời.
            _db.Entry(review).State = EntityState.Detached;
            if (await HasReviewedAsync(userId, productId))
            {
                return (false, DuplicateMessage);
            }

            throw;
        }

        return (true, "Cảm ơn bạn! Đánh giá đang chờ kiểm duyệt.");
    }

    public async Task<IReadOnlyList<Review>> GetApprovedAsync(int productId)
    {
        return await _db.Reviews
            .Include(r => r.User)
            .Where(r => r.ProductId == productId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ReviewSummary> GetSummaryAsync(int productId)
    {
        var ratings = await _db.Reviews
            .Where(r => r.ProductId == productId && r.IsApproved)
            .Select(r => r.Rating)
            .ToListAsync();

        return ratings.Count == 0
            ? new ReviewSummary(0m, 0)
            : new ReviewSummary(RoundAverage(ratings), ratings.Count);
    }

    public async Task<IReadOnlyDictionary<int, ReviewSummary>> GetSummariesAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, ReviewSummary>();
        }

        var rows = await _db.Reviews
            .Where(r => ids.Contains(r.ProductId) && r.IsApproved)
            .Select(r => new { r.ProductId, r.Rating })
            .ToListAsync();

        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => new ReviewSummary(RoundAverage(g.Select(r => r.Rating)), g.Count()));
    }

    public async Task<IReadOnlyList<Review>> GetAllAsync(bool? approved = null)
    {
        var query = _db.Reviews
            .Include(r => r.Product)
            .Include(r => r.User)
            .AsNoTracking();

        if (approved.HasValue)
        {
            query = query.Where(r => r.IsApproved == approved.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is null)
        {
            return (false, NotFoundMessage);
        }

        review.IsApproved = true;
        await _db.SaveChangesAsync();
        return (true, "Đã duyệt đánh giá.");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is null)
        {
            return (false, NotFoundMessage);
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        return (true, "Đã xóa đánh giá.");
    }

    private Task<bool> HasCompletedOrderAsync(string userId, int productId)
    {
        return _db.Orders.AnyAsync(o =>
            o.UserId == userId
            && o.Status == OrderStatus.Completed
            && o.Items.Any(i => i.ProductId == productId));
    }

    private static decimal RoundAverage(IEnumerable<int> ratings) =>
        Math.Round((decimal)ratings.Average(), 1, MidpointRounding.AwayFromZero);
}
