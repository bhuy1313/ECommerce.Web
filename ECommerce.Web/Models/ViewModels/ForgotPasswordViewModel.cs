using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
