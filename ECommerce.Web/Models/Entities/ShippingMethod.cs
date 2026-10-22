namespace ECommerce.Web.Models.Entities;

public class ShippingMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Fee { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
