# Report tổng hợp — Phase 10 P0 (Task A1–A10)

> Tài liệu **tổng hợp**, không phải report gốc của SDD. Nguồn:
> `.superpowers/sdd/2026-10-18-ecommerce-v2-core-commerce/{progress.md,task-A*-report.md}` + `git log`.
> Report gốc nằm ngoài git (`.superpowers/sdd/` bị ignore cục bộ).

- **Plan:** `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md`
- **Base:** `44a11f6` → **HEAD:** `8eb0ef3`
- **Test:** 114 pass khi kết thúc đợt (tiếp tục lên 130 sau đợt review fixes — xem report riêng).

## Kết quả theo task

| Task | Nội dung | Commit | Ghi chú |
|---|---|---|---|
| A1 | Thông tin giao hàng trên `Order` + migration | `229a62e` | 4 trường shipping, NOT NULL có default |
| A2 | `ShippingDetails` + `CheckoutViewModel`, đổi chữ ký service | `147de0e` | 4 call site cũ được cập nhật |
| A3 | Nối form checkout vào ViewModel | `44090c4` | kèm fix route view `b04d375` |
| A4 | Hiển thị thông tin nhận hàng ở 2 trang chi tiết | `60da79c` | |
| A5 | `Product.ImageUrl` + quản lý ở Admin | `05049a3` | có placeholder SVG |
| A6 | Truyền `ImageUrl` xuyên catalog/cart/checkout | `6a345b3` | bỏ ảnh hardcode |
| A7 | `PaymentTransaction` + abstraction gateway | `7e44bf1` | unique index `(Provider, TransactionRef)` |
| A8 | `VnPayGateway` + `CodPaymentGateway` | `8d76e57` | verify chữ ký VNPay |
| A9 | `PaymentService` idempotent + chuyển trạng thái | `887e3b8` | callback không hồi sinh đơn huỷ |
| A10 | UI chọn phương thức + return/IPN + trang kết quả | `8eb0ef3` | COD end-to-end |

## Ghi chú

- Đợt review sau A (7 lỗi self-review) tách riêng: `2026-10-18-phase-a-review-fixes.md`.
- B1–B5 (P1) có plan và report riêng trong cùng thư mục.
