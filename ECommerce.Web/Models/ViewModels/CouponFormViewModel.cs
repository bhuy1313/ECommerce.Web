using System.ComponentModel.DataAnnotations;
using ECommerce.Web.Models.Entities.Enums;

namespace ECommerce.Web.Models.ViewModels;

public class CouponFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã giảm giá.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Mã giảm giá phải từ 3 đến 50 ký tự.")]
    [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Mã giảm giá chỉ được chứa chữ, số, gạch ngang và gạch dưới.")]
    [Display(Name = "Mã giảm giá")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Loại giảm giá")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Range(0.01, 1_000_000_000, ErrorMessage = "Giá trị giảm phải lớn hơn 0.")]
    [Display(Name = "Giá trị")]
    public decimal Value { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Giá trị đơn tối thiểu không hợp lệ.")]
    [Display(Name = "Đơn tối thiểu")]
    public decimal MinOrderAmount { get; set; }

    [Range(1, 1_000_000, ErrorMessage = "Số lượt dùng phải từ 1 đến 1.000.000.")]
    [Display(Name = "Số lượt dùng")]
    public int UsageLimit { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly? StartsOn { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
    [Display(Name = "Ngày kết thúc")]
    public DateOnly? EndsOn { get; set; }

    [Display(Name = "Đang kích hoạt")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiscountType == DiscountType.Percentage && Value > 100m)
        {
            yield return new ValidationResult(
                "Phần trăm giảm phải từ 1 đến 100.",
                new[] { nameof(Value) });
        }

        if (StartsOn.HasValue && EndsOn.HasValue && EndsOn.Value < StartsOn.Value)
        {
            yield return new ValidationResult(
                "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.",
                new[] { nameof(EndsOn) });
        }
    }
}
