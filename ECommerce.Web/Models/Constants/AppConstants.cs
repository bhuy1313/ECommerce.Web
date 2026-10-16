namespace ECommerce.Web.Models.Constants;

public static class AppConstants
{
    /// <summary>
    /// Ngưỡng cảnh báo tồn kho ít: sản phẩm có <see cref="Entities.Product.StockQuantity"/>
    /// dưới ngưỡng này được xem là sắp hết. Dùng chung cho Admin Dashboard,
    /// trang Quản lý Sản phẩm và mọi cảnh báo low-stock khác (single source of truth).
    /// </summary>
    public const int LowStockThreshold = 10;
}
