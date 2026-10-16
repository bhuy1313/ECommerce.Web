namespace ECommerce.Web.Models.Constants;

public static class AppRoles
{
    public const string Admin = "Admin";
    /// <summary>
    /// Vận hành cửa hàng (Admin-lite): được quản lý Catalog (Product/Category)
    /// và Đơn hàng (xem + cập nhật trạng thái order), KHÔNG được chạm Dashboard
    /// doanh thu, Users/phân quyền và trạng thái thanh toán.
    /// Quyền ép buộc bằng <c>[Authorize]</c> từng action trong AdminController;
    /// sidebar _AdminLayout chỉ ẩn menu (defense-in-depth).
    /// </summary>
    public const string StoreOperator = "StoreOperator";
    public const string Customer = "Customer";
    /// <summary>
    /// Dùng cho <c>[Authorize(Roles = ...)]</c> các action Operator được phép.
    /// </summary>
    public const string AdminOrOperator = Admin + "," + StoreOperator;
}
