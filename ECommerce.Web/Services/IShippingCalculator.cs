using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Services;

public interface IShippingCalculator
{
    decimal TaxRate { get; }
    Task<IReadOnlyList<ShippingMethod>> GetActiveMethodsAsync();
    Task<ShippingQuote?> QuoteAsync(int? shippingMethodId, decimal discountedSubtotal);
}
