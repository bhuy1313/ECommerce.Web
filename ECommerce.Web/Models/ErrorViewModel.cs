namespace ECommerce.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCode { get; set; } = 500;
    public string? Message { get; set; }
    public string? OriginalPath { get; set; }

    public string Title => StatusCode switch
    {
        404 => "Không tìm thấy trang (404)",
        403 => "Truy cập bị từ chối (403)",
        400 => "Yêu cầu không hợp lệ (400)",
        _ => "Đã xảy ra sự cố kỹ thuật (500)"
    };

    public string Description => StatusCode switch
    {
        404 => "Trang web hoặc sản phẩm bạn đang tìm kiếm không tồn tại, đường dẫn có thể đã bị thay đổi hoặc đã bị gỡ bỏ.",
        403 => "Tài khoản của bạn không có đủ thẩm quyền để truy cập vào khu vực này. Nếu bạn là quản trị viên, vui lòng đăng nhập bằng tài khoản Admin.",
        400 => "Yêu cầu từ trình duyệt không hợp lệ hoặc thiếu tham số xử lý cần thiết.",
        _ => "Hệ thống gặp sự cố ngoài dự kiến trong khi xử lý yêu cầu của bạn. Chi tiết lỗi đã được ghi lại tự động vào hệ thống giám sát để khắc phục."
    };
}
