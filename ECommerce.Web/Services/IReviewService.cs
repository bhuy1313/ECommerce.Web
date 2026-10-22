using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Services;

public interface IReviewService
{
    Task<bool> CanReviewAsync(string userId, int productId);
    Task<bool> HasReviewedAsync(string userId, int productId);
    Task<(bool Success, string Message)> CreateAsync(string userId, int productId, int rating, string? comment);
    Task<IReadOnlyList<Review>> GetApprovedAsync(int productId);
    Task<ReviewSummary> GetSummaryAsync(int productId);
    Task<IReadOnlyDictionary<int, ReviewSummary>> GetSummariesAsync(IEnumerable<int> productIds);
    Task<IReadOnlyList<Review>> GetAllAsync(bool? approved = null);
    Task<(bool Success, string Message)> ApproveAsync(int id);
    Task<(bool Success, string Message)> DeleteAsync(int id);
}
