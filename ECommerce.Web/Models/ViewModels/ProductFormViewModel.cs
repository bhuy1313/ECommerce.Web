using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ECommerce.Web.Models.ViewModels;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên sản phẩm không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên sản phẩm")]
    public string Name { get; set; } = string.Empty;

    [RegularExpression(@"^(https?://\S+|/\S*)$", ErrorMessage = "Ảnh đại diện phải là đường dẫn URL (http/https) hoặc đường dẫn tương đối bắt đầu bằng '/'.")]
    [StringLength(1000, ErrorMessage = "Đường dẫn ảnh không được vượt quá 1000 ký tự.")]
    [Display(Name = "Ảnh sản phẩm (URL)")]
    public string? ImageUrl { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả chi tiết")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Giá sản phẩm là bắt buộc.")]
    [Range(0.01, 10000000, ErrorMessage = "Giá sản phẩm phải lớn hơn 0 và tối đa 10,000,000.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Đơn giá ($)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Số lượng tồn kho là bắt buộc.")]
    [Range(0, 100000, ErrorMessage = "Số lượng tồn kho phải từ 0 trở lên.")]
    [Display(Name = "Số lượng tồn kho")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục cho sản phẩm.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn một danh mục hợp lệ.")]
    [Display(Name = "Danh mục")]
    public int CategoryId { get; set; }

    [Display(Name = "Đang kinh doanh (Kích hoạt)")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem>? CategoryOptions { get; set; }
}
