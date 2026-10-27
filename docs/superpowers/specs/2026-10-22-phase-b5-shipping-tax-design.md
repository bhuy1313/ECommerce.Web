# Phase B5 — Phí vận chuyển + thuế (thiết kế)

**Ngày:** 2026-10-22
**Phạm vi:** phương thức vận chuyển có phí do admin cấu hình, thuế VAT theo cấu hình, tính lại ở server khi tạo đơn và hiển thị trong checkout/chi tiết đơn/email.

## Quyết định thiết kế

- **D-B5-1 — Entity `ShippingMethod`:** `Id`, `Name` (bắt buộc, tối đa 100), `Description` (tùy chọn, tối đa 300), `Fee` (decimal 18,2, >= 0), `IsActive`, `DisplayOrder` (int). Danh sách hoạt động sắp theo `DisplayOrder`, `Fee`, `Id`.
- **D-B5-2 — Dữ liệu mặc định:** migration seed 2 phương thức: `Giao hàng tiêu chuẩn` phí `30.000`, `Giao hàng nhanh` phí `60.000`, cùng `IsActive = true`, `DisplayOrder` 1/2. Admin sửa/xóa được; không seed lại sau khi xóa.
- **D-B5-3 — Thuế:** `TaxOptions { Rate }`, section `"Tax"` trong `appsettings.json`, mặc định `0.10` (10%). Rate được kẹp trong `[0, 1]` khi dùng. Không lưu rate vào đơn — chỉ lưu số tiền thuế tại thời điểm đặt.
- **D-B5-4 — Công thức:** `taxable = max(0, subtotal - discount)`; `tax = Round(taxable * rate, 0, AwayFromZero)`; `shippingFee = method.Fee`; `total = taxable + shippingFee + tax`. Coupon vẫn tính trên `subtotal` (trước phí/thuế) như B3.
- **D-B5-5 — Checkout GET:** chọn sẵn phương thức đầu tiên đang hoạt động; nếu chưa có phương thức nào → hiển thị lỗi `"Hiện chưa có phương thức vận chuyển khả dụng."`, phí/thuế = 0 và nút đặt hàng bị vô hiệu hóa.
- **D-B5-6 — Server là nguồn chân lý:** `OrderService.CreateOrderFromCartAsync` nhận thêm `int? shippingMethodId`, tự gọi `IShippingCalculator.QuoteAsync` bên trong transaction. Client không gửi phí/thuế; nếu phương thức không tồn tại/không hoạt động → rollback, không trừ kho, không tạo đơn, trả `"Vui lòng chọn phương thức vận chuyển hợp lệ."`.
- **D-B5-7 — Snapshot trên `Order`:** thêm `ShippingFee`, `TaxAmount` (decimal 18,2, mặc định 0). `TotalAmount` đã bao gồm hai khoản này. Không thêm FK/tên phương thức vào đơn (đổi tên/xóa method không ảnh hưởng đơn cũ).
- **D-B5-8 — Hiển thị:** checkout có khối chọn phương thức (radio, phí mỗi phương thức), dòng `Phí vận chuyển`, dòng `Thuế VAT (10%)`, tổng thanh toán = tạm tính − giảm giá + phí + thuế. Chi tiết đơn (khách + admin) và email xác nhận hiển thị phí vận chuyển/thuế từ snapshot. Trang sản phẩm đổi ghi chú thành "Giá chưa bao gồm thuế VAT và phí vận chuyển."
- **D-B5-9 — Admin:** `/Admin/ShippingMethods` (list), `CreateShippingMethod` GET/POST, `EditShippingMethod` GET/POST, `ToggleShippingMethodStatus` POST, `DeleteShippingMethod` POST — tất cả Admin-only + anti-forgery. Danh sách hiển thị thuế suất hiện hành (chỉ đọc). Nút "Vận chuyển" ở dashboard.
- **D-B5-10 — Thông báo:** `"Đã tạo phương thức vận chuyển."`, `"Đã cập nhật phương thức vận chuyển."`, `"Đã xóa phương thức vận chuyển."`, `"Đã cập nhật trạng thái phương thức vận chuyển."`, `"Không tìm thấy phương thức vận chuyển."`.
- **D-B5-11 — Service API:**
```csharp
public sealed record ShippingQuote(int ShippingMethodId, string ShippingMethodName, decimal ShippingFee, decimal TaxRate, decimal TaxAmount, decimal Total);
public interface IShippingCalculator
{
    decimal TaxRate { get; }
    Task<IReadOnlyList<ShippingMethod>> GetActiveMethodsAsync();
    Task<ShippingQuote?> QuoteAsync(int? shippingMethodId, decimal discountedSubtotal);
}
```
- **D-B5-12 — Không đổi:** coupon, stock, payment, email flow; đơn cũ có `ShippingFee = TaxAmount = 0`.

## Ngoài phạm vi

- Miễn phí ship theo ngưỡng, phí theo khu vực/cân nặng, nhiều loại thuế, VAT inclusive.
- Sửa thuế suất trên UI admin (đọc từ cấu hình).
- Hoàn phí ship khi hủy đơn (chỉ hoàn kho như hiện tại).
