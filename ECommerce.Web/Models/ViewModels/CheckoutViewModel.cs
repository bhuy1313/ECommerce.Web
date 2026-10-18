using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100)]
    [Display(Name = "Người nhận")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(15)]
    [Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
    [StringLength(300)]
    [Display(Name = "Địa chỉ giao hàng")]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public CartViewModel Cart { get; set; } = new();
}
