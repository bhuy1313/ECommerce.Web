using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;

namespace ECommerce.Web.Services;

public interface ICouponService
{
    Task<CouponValidationResult> ValidateAsync(string? code, decimal subtotal);
    Task<CouponValidationResult> RedeemAsync(string? code, decimal subtotal);
    Task<IReadOnlyList<Coupon>> GetAllAsync();
    Task<Coupon?> GetByIdAsync(int id);
    Task<(bool Success, string Message)> CreateAsync(CouponFormViewModel model);
    Task<(bool Success, string Message)> UpdateAsync(CouponFormViewModel model);
    Task<(bool Success, string Message)> DeleteAsync(int id);
    Task<(bool Success, string Message)> ToggleActiveAsync(int id);
}
