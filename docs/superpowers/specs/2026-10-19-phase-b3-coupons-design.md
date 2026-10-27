# Phase B3 — Coupon / mã giảm giá (design)

**Ngày:** 2026-10-19
**Phạm vi:** roadmap `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` Task B3 @649.
**Trạng thái:** user bỏ qua phiên hỏi brainstorming; quyết định dưới đây là của agent.

## Mục tiêu

Admin tạo được mã giảm giá (phần trăm hoặc số tiền cố định, có điều kiện đơn tối thiểu,
thời hạn, số lượt dùng). Khách nhập mã ở bước thanh toán, thấy số tiền giảm trước khi đặt,
và server kiểm tra + trừ lượt dùng **bên trong transaction tạo đơn** để không bán quá số lượt.

## Quyết định thiết kế

- **D-B3-1 — Entity `Coupon`.** `Id`, `Code` (max 50, unique, chuẩn hóa `Trim().ToUpperInvariant()`),
  `DiscountType`, `Value` (decimal 18,2), `MinOrderAmount` (decimal 18,2), `UsageLimit` (int),
  `UsedCount` (int), `StartsOn`/`EndsOn` (`DateOnly`, inclusive, so theo ngày UTC),
  `IsActive` (bool). Enum `DiscountType { Percentage = 1, FixedAmount = 2 }`.
- **D-B3-2 — Cách tính giảm.** Percentage: `Value` phải 1..100, giảm
  `Round(subtotal * Value / 100, 0, AwayFromZero)`; FixedAmount: `Value > 0`, giảm `Value`.
  Cả hai **cap ở subtotal** để tổng không âm. Làm tròn 0 chữ số thập phân (VND).
- **D-B3-3 — Order lưu snapshot.** Thêm `Order.DiscountAmount` (decimal 18,2, NOT NULL, default 0)
  và `Order.CouponCode` (max 50, nullable). `TotalAmount = subtotal - DiscountAmount`.
  Không khóa ngoại tới `Coupon`: sửa/xóa mã không làm đổi đơn cũ.
- **D-B3-4 — Hai bước validate.** Preview (`GET /Cart/Checkout?couponCode=...`) gọi
  `ICouponService.ValidateAsync` (chỉ đọc). Mã được mang sang POST bằng hidden input.
  `OrderService` gọi `ICouponService.RedeemAsync` **trong transaction hiện có**, dùng
  `ExecuteUpdateAsync` có điều kiện (`UsedCount < UsageLimit`, còn hiệu lực, đủ đơn tối thiểu)
  để tăng lượt dùng atomic. `affected == 0` ⇒ rollback, báo lỗi, không tạo đơn.
- **D-B3-5 — Một mã mỗi đơn.** Không xếp chồng mã; `UsageLimit` là tổng toàn hệ thống,
  không giới hạn theo từng user (ngoài phạm vi B3).
- **D-B3-6 — Admin CRUD** trong `AdminController` (`/Admin/Coupons`, `CouponForm`,
  `CreateCoupon`, `EditCoupon`, `ToggleCouponStatus`, `DeleteCoupon`), `[Authorize(Roles = Admin)]`.
  Xóa mã đã dùng vẫn được (đơn giữ `CouponCode` snapshot); `UsedCount` không reset.
- **D-B3-7 — Hiển thị.** Preview có ô nhập mã + dòng "Giảm giá" + tổng phải trả; trang chi tiết
  đơn (khách + admin) và email xác nhận hiện dòng giảm giá khi `DiscountAmount > 0`.
- **D-B3-8 — Thông báo lỗi tiếng Việt**, không phân biệt mã không tồn tại với mã bị vô hiệu hóa:
  "Mã giảm giá không tồn tại hoặc đã bị vô hiệu hóa."; chưa hiệu lực: "Mã giảm giá chưa có hiệu lực.";
  hết hạn: "Mã giảm giá đã hết hạn."; hết lượt: "Mã giảm giá đã được sử dụng hết.";
  đơn tối thiểu: "Đơn hàng chưa đạt giá trị tối thiểu để dùng mã này."
- **D-B3-9 — Không thêm NuGet package**, giữ ASP.NET Core MVC + EF Core 10; migration `AddCoupons`;
  `MigrateAsync()` sẵn có tự áp dụng.

## Luồng

1. Admin tạo mã tại `/Admin/Coupons` (mã, loại, giá trị, đơn tối thiểu, số lượt, ngày bắt đầu/kết thúc, active).
2. Khách ở `/Cart/Checkout`: nhập mã vào form GET "Áp dụng" → server validate read-only → hiện
   số tiền giảm hoặc lỗi; mã hợp lệ nằm trong hidden input của form đặt hàng.
3. `POST /Orders/Checkout`: validate model → `OrderService.CreateOrderFromCartAsync(..., couponCode)`
   → trong transaction: kiểm tra kho → redeem coupon atomic → tạo order + items + xóa giỏ.
   Coupon lỗi ⇒ rollback toàn bộ, redirect lại `/Cart/Checkout?couponCode=...` kèm toast lỗi.
4. Đơn lưu `DiscountAmount`/`CouponCode`; tổng tiền và thanh toán (VNPay) dùng số đã giảm.

## Kiểm thử

- `CouponServiceTests`: phần trăm, cố định, cap subtotal, làm tròn; mã trống; không tồn tại/inactive/
  chưa bắt đầu/hết hạn/hết lượt/chưa đủ đơn tối thiểu; redeem tăng `UsedCount`; redeem lần 2 khi
  `UsageLimit = 1` thất bại; chuẩn hóa chữ thường/khoảng trắng; CRUD admin (tạo, trùng mã, sửa,
  toggle, xóa).
- `OrderServiceTests`: áp mã hợp lệ (total/discount/code + UsedCount++); mã không hợp lệ hủy đơn,
  giỏ và kho nguyên vẹn; redeem thất bại khi đã hết lượt.
- `CartControllerCheckoutTests`: preview mã hợp lệ/lỗi/trống.
- `CheckoutControllerTests`: `couponCode` được truyền vào `CreateOrderFromCartAsync`; coupon lỗi
  redirect về Checkout kèm mã.
- Live verify: tạo mã qua admin, áp mã ở checkout (thấy giảm), đặt đơn, kiểm tra UsedCount + dòng
  giảm giá trong chi tiết đơn/email log; dùng lại mã hết lượt bị chặn; teardown dev server.

## Review Focus

- Mã trống/khoảng trắng phải y hệt "không dùng mã" (không lỗi, không giảm).
- Hai checkout đồng thời với `UsageLimit = 1`: chỉ một đơn thành công, đơn kia rollback, kho/giỏ nguyên.
- Mã hợp lệ lúc preview nhưng hết hạn/hết lượt/bị tắt lúc POST: đơn hủy, giỏ còn nguyên, có thông báo.
- Giảm cố định lớn hơn subtotal: tổng không âm, giảm tối đa bằng subtotal.
- Admin tạo mã trùng (khác hoa/thường), ký tự lạ, phần trăm > 100, ngày kết thúc trước ngày bắt đầu:
  bị từ chối, không ghi DB.

## Ngoài phạm vi

Xếp chồng nhiều mã, giới hạn lượt theo từng user, mã gắn danh mục/sản phẩm, mã tự động/khuyến mãi
theo chiến dịch, hoàn lượt dùng khi hủy đơn.
