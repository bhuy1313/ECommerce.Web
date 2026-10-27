# Report - Phase B5: Shipping & tax

**Ngày:** 2026-10-22
**Plan:** `docs/superpowers/plans/2026-10-22-phase-b5-shipping-tax.md`
**Spec:** `docs/superpowers/specs/2026-10-22-phase-b5-shipping-tax-design.md`

## Kết quả

- Commit: `bfaa5f7` (entity + calculator + migration), `30dfab3` (checkout chọn phương thức + preview), `62f9455` (snapshot phí/thuế trên đơn), `ae418fe` (admin quản lý phương thức vận chuyển), `46edad9` (sửa findings review: rate limit admin + copy giỏ hàng).
- Baseline trước B5: 241 test. Sau: **266/266 test pass** (+25, gồm 5 test hồi quy cho review fix).
- Build `dotnet build ECommerce.Web.slnx --no-incremental`: **0 warning, 0 error**.
- Migration đã áp vào dev LocalDB: `20261022074934_AddShippingMethods` (seed 2 phương thức) và `20261022081235_AddOrderShippingTax` (`ShippingFee`, `TaxAmount` default 0).

## Thay đổi chính

- `ShippingMethod`: tên ≤100, mô tả ≤300, phí, `IsActive`, `DisplayOrder`; seed “Giao hàng tiêu chuẩn” 30.000 (order 1) và “Giao hàng nhanh” 60.000 (order 2).
- `ShippingCalculator` + `TaxOptions`: chỉ trả phương thức đang hoạt động; quote `max(0, subtotal - discount)`; thuế `Round(taxable * rate, 0, AwayFromZero)`; rate bị kẹp `[0,1]`; tổng = taxable + phí + thuế.
- Checkout GET: chọn phương thức đầu tiên theo `DisplayOrder`, hiển thị radio + phí + VAT + tổng; không có phương thức thì báo lỗi và disable nút đặt hàng.
- `OrderService` tự quote lại trong transaction sau khi áp coupon; client chỉ gửi `ShippingMethodId`. Phương thức sai/bị tắt → rollback, không trừ kho, message “Vui lòng chọn phương thức vận chuyển hợp lệ.”.
- `Order` lưu snapshot `ShippingFee`/`TaxAmount`; đơn cũ mặc định 0. Chi tiết đơn (khách + admin) và email xác nhận hiển thị phí/thuế.
- Admin `/Admin/ShippingMethods` (Admin-only): list kèm thuế suất hiện hành, tạo/sửa/bật-tắt/xóa; nút “Vận chuyển” trên dashboard. Thuế suất chỉ đọc từ `appsettings.json` (`Tax:Rate`).

## Live verify (một lần boot, teardown sạch)

Sản phẩm 975.000; phương thức live phí 25.000; thuế 10% = 97.500; tổng 1.097.500. **21/21 CHECK = True, `FAILED_CHECKS=0`**:

```
admin_login_ok, admin_seed_methods_listed, admin_create_method_ok,
admin_edit_method_ok, buyer_register_ok, buyer_add_to_cart, cart_copy_ok,
checkout_shows_method, checkout_shows_shipping_fee, checkout_shows_tax,
checkout_shows_payable_total, order_created, order_fee_tax_total_persisted,
order_details_show_fees, email_contains_fees, forged_shipping_rejected,
forged_shipping_no_side_effects, no_method_blocks_checkout,
admin_toggle_method_ok, admin_delete_method_ok, teardown_clean
```

Đã kiểm tra: phí/thuế/tổng hiển thị và lưu DB khớp; details + email có phí/thuế; POST forged `ShippingMethodId=999999` trả lại form lỗi, không tạo đơn, không đổi kho, giỏ hàng còn nguyên; tắt toàn bộ phương thức → checkout báo lỗi và nút bị disable; admin sửa/tắt/bật/xóa phương thức hoạt động; giỏ hàng hiển thị “Tổng tạm tính” và ghi chú chưa gồm phí/thuế. Port 5080 không còn listen sau khi chạy.

## Side effect dev DB

- Orders `21014`, `22014`, `22015`, `22016` (COD, phí 25.000, thuế 97.500, tổng 1.097.500) — phát sinh từ 4 lần chạy live verify trong quá trình sửa script kiểm tra định dạng tiền tệ và bổ sung bước sửa phương thức.
- Users `b5.1792657524@technova.local`, `b5.1792657583@technova.local`, `b5.1792657642@technova.local`, `b5.1792660418@technova.local`, mật khẩu `Qa!23456`.
- `ShippingMethods` còn đúng 2 bản ghi seed, đều `IsActive=1`; phương thức `B5LIVE...` đã bị xóa trong live verify.

## Mutation checks

- Bỏ clamp thuế (`TaxRate = options.Rate`) → `TaxRate_OutsideRange_IsClamped` fail (expected 1, actual 1.5).
- Bỏ cộng phí/thuế vào `PayableTotal` → test checkout fail.
- Trừ thuế khỏi `TotalAmount` khi tạo đơn → `OrderServiceTests` fail.
- Tất cả đã khôi phục; full suite xanh lại 266/266.

## Graph gate

`node .gitnexus/run.cjs analyze --index-only` → 2.138 nodes / 4.798 edges / 74 clusters / 96 flows.

`node .gitnexus/run.cjs detect-changes --scope compare --base-ref 1f35832`:

- 35 files / 138 symbols / 16 execution flows, **risk critical**, không `partial`/`truncated`.
- **Cảnh báo:** risk critical do thay đổi luồng checkout lõi (`Checkout`, `CreateOrderFromCartAsync`, `QuoteAsync`, `PopulateCheckoutAsync`) và nhiều flow con. Đã bao phủ bằng 266 unit/integration test + 21 live check, gồm forged request và trạng thái không có phương thức; không có luồng chưa xác minh.

### Review fix (`46edad9`)

`detect-changes --scope compare --base-ref ae418fe` → 4 files / 26 symbols / 6 flows, **risk high**, không `partial`/`truncated`. Risk high do gắn `[EnableRateLimiting("mutating")]` lên toàn bộ 19 POST của `AdminController`; chỉ đổi attribute, không đổi thân action. Đã xác minh bằng reflection test (19/19 action có policy), 5 test hồi quy admin edit, full suite 266/266 và live verify 21/21.

## Ghi chú

- `ShippingCalculator.TaxRate` được clamp ngay trong constructor để mọi consumer (checkout, order, admin) dùng cùng một giá trị.
- `OrdersController` validate lại `ShippingMethodId` trước khi gọi service; `OrderService` vẫn quote lại trong transaction (defense-in-depth) nên request forged/bị tắt không thể tạo đơn.
- Coupon vẫn tính trên subtotal gốc; thuế và phí tính trên subtotal sau giảm.
- B5 không thêm FK/tên phương thức vào `Order` — chỉ snapshot số tiền, nên admin sửa/xóa phương thức không làm đổi đơn cũ.
- `Views/Cart/Index.cshtml`: phí ship đổi thành “Tính ở bước thanh toán”; tổng cuối đổi thành “Tổng tạm tính” kèm ghi chú “Chưa gồm phí vận chuyển và thuế VAT” để không mâu thuẫn với VAT cộng ở checkout.
