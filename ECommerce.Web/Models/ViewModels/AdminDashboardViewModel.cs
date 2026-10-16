using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Models.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int ShippedOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }

    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalCategories { get; set; }
    public int LowStockCount { get; set; }
    public int TotalUsers { get; set; }

    public List<Order> RecentOrders { get; set; } = new();
    public List<Product> LowStockProducts { get; set; } = new();
}
