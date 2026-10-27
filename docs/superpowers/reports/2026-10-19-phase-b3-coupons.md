# Report - Phase B3: Coupon / mã giảm giá

**Ngày:** 2026-10-19
**Plan:** `docs/superpowers/plans/2026-10-19-phase-b3-coupons.md`
**Spec:** `docs/superpowers/specs/2026-10-19-phase-b3-coupons-design.md`

## Kết quả

- Commit: `910b49d` (entity + service + migration), `4290f83` (redeem khi tạo đơn), `6928abb` (preview ở checkout), `b76fc49` (quản trị coupon), `8f89825` (hiển thị giảm giá ở chi tiết đơn/email).
- Baseline trước: 201 test. Sau: **206/206 test pass** (+5 InlineData cho ma trận phân quyền admin).
- Build `dotnet build ECommerce.Web.slnx --no-incremental`: **0 warning, 0 error**.
- Migration `20261019021145_AddCoupons` (bảng `Coupons` + unique index `Code`) và `20261019022020_AddCouponOrderSnapshot` (`Orders.CouponCode`, `Orders.DiscountAmount` default 0) đã áp vào dev LocalDB.

## Thay đổi chính

- `Coupon` + `DiscountType { Percentage, FixedAmount }`; mã chuẩn hóa `Trim().ToUpperInvariant()`; hiệu lực theo `DateOnly` UTC, `UsageLimit`, `MinOrderAmount`, `IsActive`.
- `ICouponService`/`CouponService`: `ValidateAsync` (read-only) và `RedeemAsync` (atomic `ExecuteUpdateAsync` với filter đầy đủ, detach entity sau update); CRUD cho admin.
- Checkout: `GET /Cart/Checkout?couponCode=...` preview giảm giá + `PayableTotal`; POST đơn mang `CouponCode`, server validate + redeem trong transaction tạo đơn; đơn lưu snapshot `CouponCode`/`DiscountAmount`, không FK.
- Admin: `/Admin/Coupons`, `/Admin/CreateCoupon`, `/Admin/EditCoupon`, toggle/xóa — Admin-only, đã đưa vào ma trận phân quyền.
- Hiển thị giảm giá ở `/Orders/Details`, `/Admin/OrderDetails`, email xác nhận đơn.

## Live verify (một lần boot, teardown sạch)

Coupon `B3LIVE1792377292` (10%, limit 1, hiệu lực 7 ngày), **19/19 CHECK = True, `FAILED_CHECKS=0`**:

```
admin_login_ok, admin_coupon_list_ok, admin_coupon_form_ok, admin_create_coupon,
admin_coupon_starts_active, buyer1_register_ok, preview_ok, preview_shows_code,
preview_shows_discount, order_created, order_details_show_coupon, order_snapshot,
coupon_used_once, buyer2_register_ok, preview_rejects_exhausted,
order_rejected_when_exhausted, rejected_order_cart_intact, only_one_coupon_order,
teardown_clean
```

Đơn `19014`: giá gốc 975.000, giảm 97.500, tổng 877.500; `UsedCount = 1`; buyer 2 bị chặn và giỏ còn nguyên. Port 5080 không còn listen sau khi chạy.

## Side effect dev DB

- Orders: `18014` (lần chạy đầu, check form sai route) và `19014` (lần chạy pass), note `B3 live ...`.
- Coupons: `B3LIVE1792377245`, `B3LIVE1792377292` (mỗi mã `UsedCount = 1`).
- Users: `b3.1.*`, `b3.2.*` với `*` là 2 stamp trên, mật khẩu `Qa!23456`.

## Graph gate

`node .gitnexus/run.cjs detect-changes --scope compare --base-ref c4afdc6`

- 34 files / 184 symbols / 17 execution flows, **risk critical**, không `partial`/`truncated`.
- Flows bị ảnh hưởng chính: `Checkout → ValidateAsync/Evaluate/Fail`, `CreateOrderFromCartAsync → RedeemAsync`, `CreateCoupon → CreateAsync/Map`.
- Index được rebuild trước khi gate (`analyze --index-only`, 1.834 nodes / 3.995 edges).

## Ghi chú

- Lần chạy live đầu tiên fail 1 check vì script gọi `/Admin/CouponForm`; route đúng là `GET /Admin/CreateCoupon` (view `CouponForm`). Không phải lỗi ứng dụng.
- `AdminAuthorizationTests` phải cập nhật khi thêm 5 action coupon (test khóa ma trận Admin-only).
- `RedeemAsync` detach coupon sau `ExecuteUpdateAsync` để tránh change tracker trả `UsedCount` cũ.
