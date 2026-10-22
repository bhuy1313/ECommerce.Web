using ECommerce.Web.Data;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Web.Services;

public class ShippingCalculator : IShippingCalculator
{
    private readonly ApplicationDbContext _db;

    public ShippingCalculator(ApplicationDbContext db, IOptions<TaxOptions> taxOptions)
    {
        _db = db;
        TaxRate = Math.Clamp(taxOptions.Value.Rate, 0m, 1m);
    }

    public decimal TaxRate { get; }

    public async Task<IReadOnlyList<ShippingMethod>> GetActiveMethodsAsync()
    {
        return await _db.ShippingMethods
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Fee)
            .ThenBy(m => m.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ShippingQuote?> QuoteAsync(int? shippingMethodId, decimal discountedSubtotal)
    {
        if (!shippingMethodId.HasValue)
        {
            return null;
        }

        var method = await _db.ShippingMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == shippingMethodId.Value && m.IsActive);

        if (method is null)
        {
            return null;
        }

        var taxable = Math.Max(0m, discountedSubtotal);
        var tax = Math.Round(taxable * TaxRate, 0, MidpointRounding.AwayFromZero);

        return new ShippingQuote(
            method.Id,
            method.Name,
            method.Fee,
            TaxRate,
            tax,
            taxable + method.Fee + tax);
    }
}
