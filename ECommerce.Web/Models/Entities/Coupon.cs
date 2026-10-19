using ECommerce.Web.Models.Entities.Enums;

namespace ECommerce.Web.Models.Entities;

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; }
}
