using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services;

public class CouponService : ICouponService
{
    private const string NotFoundMessage = "Mã giảm giá không tồn tại.";
    private const string DuplicateMessage = "Mã giảm giá đã tồn tại.";
    private const string InactiveMessage = "Mã giảm giá không tồn tại hoặc đã bị vô hiệu hóa.";
    private const string NotStartedMessage = "Mã giảm giá chưa có hiệu lực.";
    private const string ExpiredMessage = "Mã giảm giá đã hết hạn.";
    private const string ExhaustedMessage = "Mã giảm giá đã được sử dụng hết.";
    private const string MinOrderMessage = "Đơn hàng chưa đạt giá trị tối thiểu để dùng mã này.";

    private readonly ApplicationDbContext _db;

    public CouponService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CouponValidationResult> ValidateAsync(string? code, decimal subtotal)
    {
        var (_, result) = await EvaluateAsync(code, subtotal, track: false);
        return result;
    }

    public async Task<CouponValidationResult> RedeemAsync(string? code, decimal subtotal)
    {
        var (coupon, result) = await EvaluateAsync(code, subtotal, track: true);
        if (coupon is null || !result.IsValid)
        {
            return result;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var affected = await _db.Coupons
            .Where(c => c.Id == coupon.Id
                && c.IsActive
                && c.UsedCount < c.UsageLimit
                && c.StartsOn <= today
                && c.EndsOn >= today
                && c.MinOrderAmount <= subtotal)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1));

        if (affected == 0)
        {
            return new CouponValidationResult(false, ExhaustedMessage, null, 0m, null);
        }

        // ExecuteUpdate bỏ qua change tracker; detach để lần đọc sau không thấy UsedCount cũ.
        _db.Entry(coupon).State = EntityState.Detached;

        return result;
    }

    public async Task<IReadOnlyList<Coupon>> GetAllAsync()
    {
        return await _db.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.EndsOn)
            .ThenBy(c => c.Code)
            .ToListAsync();
    }

    public Task<Coupon?> GetByIdAsync(int id)
    {
        return _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<(bool Success, string Message)> CreateAsync(CouponFormViewModel model)
    {
        var code = Normalize(model.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code))
        {
            return (false, DuplicateMessage);
        }

        _db.Coupons.Add(Map(model, new Coupon { Code = code }));
        await _db.SaveChangesAsync();
        return (true, "Đã tạo mã giảm giá.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(CouponFormViewModel model)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == model.Id);
        if (coupon is null)
        {
            return (false, NotFoundMessage);
        }

        var code = Normalize(model.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != model.Id))
        {
            return (false, DuplicateMessage);
        }

        coupon.Code = code;
        Map(model, coupon);
        await _db.SaveChangesAsync();
        return (true, "Đã cập nhật mã giảm giá.");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon is null)
        {
            return (false, NotFoundMessage);
        }

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();
        return (true, "Đã xóa mã giảm giá.");
    }

    public async Task<(bool Success, string Message)> ToggleActiveAsync(int id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon is null)
        {
            return (false, NotFoundMessage);
        }

        coupon.IsActive = !coupon.IsActive;
        await _db.SaveChangesAsync();
        return (true, "Đã cập nhật trạng thái mã giảm giá.");
    }

    private async Task<(Coupon? Coupon, CouponValidationResult Result)> EvaluateAsync(
        string? code, decimal subtotal, bool track)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (null, new CouponValidationResult(false, null, null, 0m, null));
        }

        var normalized = Normalize(code);
        var query = track ? _db.Coupons : _db.Coupons.AsNoTracking();
        var coupon = await query.FirstOrDefaultAsync(c => c.Code == normalized);
        return (coupon, Evaluate(coupon, subtotal));
    }

    private static CouponValidationResult Evaluate(Coupon? coupon, decimal subtotal)
    {
        if (coupon is null || !coupon.IsActive)
        {
            return Fail(InactiveMessage);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today < coupon.StartsOn)
        {
            return Fail(NotStartedMessage);
        }

        if (today > coupon.EndsOn)
        {
            return Fail(ExpiredMessage);
        }

        if (coupon.UsedCount >= coupon.UsageLimit)
        {
            return Fail(ExhaustedMessage);
        }

        if (subtotal < coupon.MinOrderAmount)
        {
            return Fail(MinOrderMessage);
        }

        var discount = coupon.DiscountType == DiscountType.Percentage
            ? Math.Round(subtotal * coupon.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : coupon.Value;
        discount = Math.Clamp(discount, 0m, subtotal);

        var description = coupon.DiscountType == DiscountType.Percentage
            ? $"Giảm {coupon.Value:N0}%"
            : $"Giảm {coupon.Value:N0} ₫";

        return new CouponValidationResult(true, null, coupon.Code, discount, description);
    }

    private static CouponValidationResult Fail(string message) =>
        new(false, message, null, 0m, null);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static Coupon Map(CouponFormViewModel model, Coupon coupon)
    {
        coupon.DiscountType = model.DiscountType;
        coupon.Value = model.Value;
        coupon.MinOrderAmount = model.MinOrderAmount;
        coupon.UsageLimit = model.UsageLimit;
        coupon.StartsOn = model.StartsOn!.Value;
        coupon.EndsOn = model.EndsOn!.Value;
        coupon.IsActive = model.IsActive;
        return coupon;
    }
}
