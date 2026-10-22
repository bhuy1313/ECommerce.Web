namespace ECommerce.Web.Models.ViewModels;

public sealed record ShippingMethodOption(int Id, string Name, decimal Fee, string? Description);
