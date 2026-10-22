using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public class ShippingMethodFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phương thức vận chuyển.")]
    [StringLength(100, ErrorMessage = "Tên phương thức tối đa 100 ký tự.")]
    [Display(Name = "Tên phương thức")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "Phí vận chuyển phải từ 0 đến 999.999.999.")]
    [Display(Name = "Phí vận chuyển (VNĐ)")]
    public decimal Fee { get; set; }

    [Range(0, 10_000, ErrorMessage = "Thứ tự hiển thị phải từ 0 đến 10.000.")]
    [Display(Name = "Thứ tự hiển thị")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}
