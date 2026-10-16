namespace ECommerce.Web.Models.ViewModels;

public class CartItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal LineTotal => Quantity * UnitPrice;
    public bool HasStockIssue => Quantity > AvailableStock || AvailableStock <= 0 || !IsActive;
}

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public int TotalItems => Items.Sum(i => i.Quantity);
    public bool HasAnyStockIssue => Items.Any(i => i.HasStockIssue);
}
