using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public class ReviewFormViewModel
{
    [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao.")]
    public int Rating { get; set; } = 5;

    [StringLength(1000, ErrorMessage = "Nội dung đánh giá tối đa 1000 ký tự.")]
    public string? Comment { get; set; }
}
