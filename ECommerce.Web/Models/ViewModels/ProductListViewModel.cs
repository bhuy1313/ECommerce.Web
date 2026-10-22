using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;

namespace ECommerce.Web.Models.ViewModels;

public class ProductListViewModel
{
    public IEnumerable<Product> Products { get; set; } = new List<Product>();
    public IEnumerable<Category> Categories { get; set; } = new List<Category>();
    public int? SelectedCategoryId { get; set; }
    public string? SearchQuery { get; set; }
    public IReadOnlyDictionary<int, ReviewSummary> Ratings { get; set; } = new Dictionary<int, ReviewSummary>();
}
