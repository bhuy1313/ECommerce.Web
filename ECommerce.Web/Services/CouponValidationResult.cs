namespace ECommerce.Web.Services;

public sealed record CouponValidationResult(
    bool IsValid,
    string? Error,
    string? Code,
    decimal DiscountAmount,
    string? Description);
