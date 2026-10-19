using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public sealed class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;
}
