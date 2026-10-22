using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Models.ViewModels;

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = null!;
    public IReadOnlyList<Review> Reviews { get; set; } = Array.Empty<Review>();
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool CanReview { get; set; }
    public bool HasReviewed { get; set; }
    public ReviewFormViewModel NewReview { get; set; } = new();
}
