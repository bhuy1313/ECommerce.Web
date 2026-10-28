# E-Commerce Roadmap

> **Cập nhật lần cuối:** 2026-10-27 · code `main` @ `46edad9` · test `266/266` PASS · production: <https://ecommerce-demo-9phe.onrender.com>
>
> Đây là **nguồn chân lý duy nhất cho tiến độ**. Bản đồ tài liệu (spec / plan / report, quy ước cập nhật) ở [`docs/README.md`](README.md).

Dựa trên `01-Project-Overview-E-Commerce.docx`, chia nhỏ theo các phase có thể phát triển tuần tự.

## Phase 0 — Setup môi trường
- [x] Xác nhận SQL Server LocalDB (đã cài sẵn, bản 2025 Express)
- [x] Thêm connection string `DefaultConnection` vào `appsettings.json`
- [x] Cài package `Microsoft.EntityFrameworkCore.SqlServer`
- [x] Cài `Microsoft.EntityFrameworkCore.Tools` (dotnet ef CLI) + `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- [x] Chốt cấu trúc: 1 project + folders (`Data/`, `Models/Entities/`, `Models/`) — theo lựa chọn

## Phase 1 — Data Layer (Domain + Infrastructure)
- [x] `DbContext` + đăng ký DI trong `Program.cs`
- [x] Entities: `Product`, `Category`, `Cart`, `CartItem`, `Order`, `OrderItem`
- [x] Enums: `OrderStatus` (Pending/Processing/Shipped/Completed/Cancelled), `PaymentStatus`
- [x] Identity entities (User, Role)
- [x] Migration đầu tiên + `Database.Migrate()` khi start
- [x] Seed data: 3 categories + 7 sản phẩm mẫu

## Phase 2 — Authentication & Authorization
- [x] Register, Login, Logout (ASP.NET Core Identity)
- [x] Roles: `Admin`, `StoreOperator`, `Customer`
- [x] Seed role Admin; gán admin từ dòng lệnh/seed
- [x] Bảo vệ các route admin bằng `[Authorize(Roles=...)]`

## Phase 3 — Catalog (Product + Category)
- [x] List sản phẩm + chi tiết (Customer)
- [x] Search + filter theo category
- [x] Admin: CRUD Category
- [x] Admin: CRUD Product + archive/deactivate sản phẩm
- [x] Validation phía server (Data Annotations)

## Phase 4 — Shopping Cart
- [x] Thêm sản phẩm vào cart
- [x] Cập nhật số lượng / xóa item
- [x] Kiểm tra stock trước khi checkout
- [x] Xử lý cart khi guest / logged-in

## Phase 5 — Checkout & Orders
- [x] Tạo Order từ cart hợp lệ (transaction: order + order items + trừ stock)
- [x] Lịch sử đơn hàng của customer
- [x] Admin: xem tất cả orders, cập nhật trạng thái
- [x] Hủy đơn theo business rule + hoàn stock
- [x] Chặn stock âm + xử lý concurrent purchase (safeguard)

## Phase 6 — Admin Dashboard
- [x] Tổng quan: doanh thu, số đơn, số sản phẩm
- [x] Danh sách đơn gần đây
- [x] Cảnh báo low-stock
- [x] Quản lý user + phân vai

## Phase 7 — Error Handling & Logging
- [x] `UseExceptionHandler` + trang lỗi thân thiện
- [x] Exception filter / middleware tập trung
- [x] Logging hợp lý (`ILogger`)

## Phase 8 — Testing
- [x] Setup project test (xUnit)
- [x] Test business logic: stock update, cancel order, order total, rule validation

## Phase 8.5 — UI Storefront Integration (TechNova Template: e_commerce_app.html)
- [x] Setup Layouts & Cách ly CSS:
  - Tạo `_AdminLayout.cshtml` + `Views/Admin/_ViewStart.cshtml` (giữ nguyên Bootstrap 5 cho trang quản trị)
  - Viết lại `Views/Shared/_Layout.cshtml` theo template TechNova (Tailwind CSS CDN, FontAwesome 6, Google Fonts Inter)
  - Tích hợp partial `_LoginPartial.cshtml` cho liên kết tài khoản; số lượng giỏ hàng lấy động từ `ICartService` trong `_Layout.cshtml` (dòng 4 + 99–104).
- [x] Ghép trang Chủ & Catalog:
  - `Views/Home/Index.cshtml` (Hero banner, danh mục hot, sản phẩm bán chạy)
  - `Views/Products/Index.cshtml` (Bộ lọc danh mục, thanh tìm kiếm, lưới sản phẩm)
  - `Views/Products/Details.cshtml` (Chi tiết sản phẩm, tồn kho, tăng giảm số lượng, thêm vào giỏ)
- [x] Ghép Giỏ hàng & Đơn hàng:
  - `Views/Cart/Index.cshtml` (Bảng giỏ hàng, cập nhật số lượng, xóa item, tóm tắt thanh toán)
  - `Views/Cart/CheckoutPreview.cshtml` (Thông tin nhận hàng, xem trước đơn hàng, form Xác nhận Đặt hàng)
  - `Views/Orders/Index.cshtml` & `Views/Orders/Details.cshtml` (Danh sách đơn mua, trạng thái đơn, hủy đơn)
- [x] Ghép bộ trang Xác thực (Auth):
  - `Views/Account/Login.cshtml` (Giao diện đăng nhập TechNova, validation)
  - `Views/Account/Register.cshtml` (Giao diện đăng ký tài khoản TechNova, validation)
- [x] Kiểm thử & Verification:
  - Build `dotnet build` và kiểm tra toàn bộ unit test `dotnet test`
  - Đảm bảo logic nghiệp vụ và 23/23 unit tests giữ vững 100% (số liệu tại thời điểm Phase 8.5; suite hiện tại 266/266 — xem đầu file)

## Phase 8.6 — UI Admin Portal Redesign (TechNova Style)
- [x] Khung Layout Admin: `Views/Shared/_AdminLayout.cshtml` (Sidebar cố định/thu gọn, Topbar, Tailwind CSS, FontAwesome 6, Inter font, Flash alerts)
- [x] Trang Dashboard Tổng quan: `Views/Admin/Index.cshtml` (KPI cards, phân tích trạng thái đơn, bảng cảnh báo low-stock, bảng đơn hàng gần đây)
- [x] Quản lý Sản phẩm: `Views/Admin/Products.cshtml` & `Views/Admin/ProductForm.cshtml` (Lọc theo danh mục, trạng thái active, form thêm/sửa sản phẩm)
- [x] Quản lý Danh mục: `Views/Admin/Categories.cshtml` & `Views/Admin/CategoryForm.cshtml` (Bảng danh mục, số lượng sp, form thêm/sửa danh mục)
- [x] Quản lý Đơn hàng: `Views/Admin/Orders.cshtml` & `Views/Admin/OrderDetails.cshtml` (Tabs phân loại trạng thái, bảng đơn, cập nhật trạng thái đơn hàng)
- [x] Quản lý Người dùng: `Views/Admin/Users.cshtml` & `Views/Admin/EditUserRoles.cshtml` (Danh sách user, số đơn, form phân quyền role an toàn)
- [x] Kiểm thử & Verification: `dotnet build` và chạy lại 23/23 unit tests (23/23 tại thời điểm Phase 8.6; hiện 266/266)

## Phase 9 — Deploy
- [x] Chốt hosting (Render Free, Docker) — hoàn thành (deploy `https://ecommerce-demo-9phe.onrender.com/`)
- [x] Triển khai production (SQLite `/tmp/ecommerce-demo.db` với `EnsureCreated` + WAL)
- [x] Bảo mật HTTP (HSTS + redirect HTTPS, bypass `/healthz` cho healthcheck)
- [x] Kiểm thử môi trường public (`/healthz` 200, `/` 200)
- [x] Tạo `appsettings.Production.json` (cấu hình mặc định prod)
- [x] Chuyển sang migration-based (MigrateAsync) thay vì `EnsureCreated` cho prod (hardening schema)

## Phase 10 — v2: Năng lực E-commerce cốt lõi còn thiếu

> Phase 0–9 (v1) đã hoàn thành đúng cam kết, nhưng đối chiếu code thực tế thì v1 là
> bản demo có nghiệp vụ đúng nhưng còn thiếu vài năng lực cốt lõi của một E-commerce
> bán hàng thật. Phase 10 tổng hợp lại và chia theo độ ưu tiên.
>
> Kế hoạch triển khai chi tiết: `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md`

### P0 — Bắt buộc (chặn nghiệp vụ bán hàng thật)
- [x] Thông tin giao hàng trên đơn: người nhận, SĐT, địa chỉ, ghi chú — nhập ở checkout và lưu vào `Order` · A1–A3
- [x] Hiển thị thông tin giao hàng ở `Orders/Details` + `Admin/OrderDetails` · A4
- [x] Ảnh sản phẩm: `Product.ImageUrl` + quản lý ở Admin + hiển thị xuyên suốt catalog/giỏ/checkout · A5–A6
- [x] Cổng thanh toán online: abstraction `IPaymentGateway` (COD + VNPay), entity `PaymentTransaction`, callback + idempotency · A7–A10
- [x] Luồng `PaymentStatus` thật (Pending → Paid/Failed/Refunded) gắn với giao dịch · A9–A10

### P1 — Quan trọng (hoàn thiện trải nghiệm & bán hàng)
- [x] Email: xác thực email, quên/đặt lại mật khẩu, email xác nhận đơn · B1
- [x] Trang profile + sổ địa chỉ người dùng · B2
- [x] Coupon / mã giảm giá + áp dụng ở checkout · B3
- [x] Reviews & ratings sản phẩm (chỉ người đã mua) + duyệt ở Admin · B4
- [x] Phương thức & phí vận chuyển + thuế (bỏ hardcode "Miễn phí") · B5

### P2 — Tăng trưởng & vận hành
- [ ] Catalog nâng cao: sort (giá/mới), lọc khoảng giá, phân trang
- [ ] Wishlist
- [ ] Reporting/export (doanh thu, top sản phẩm, theo khoảng thời gian)
- [ ] Audit log thao tác admin
- [ ] Sản phẩm liên quan / gợi ý

### Nhật ký thực thi (v1 + v2)

| Đợt | Plan | Báo cáo | Commit | Test sau đợt |
|---|---|---|---|---|
| Verify v1 (Phase 0–8.6) | `superpowers/plans/2026-10-14-verify-legacy-phases.md` | [`reports/2026-10-14-verify-legacy-phases.md`](superpowers/reports/2026-10-14-verify-legacy-phases.md) | `9256977..a1251cb` | 24/24 |
| Deploy Render | `superpowers/plans/2026-10-16-render-free-sqlite-deploy.md` | — (commit + `/healthz`) | `abb7afa..44a11f6` | — |
| v2 P0 (A1–A10) | `superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` | [`reports/2026-10-18-ecommerce-v2-core-commerce.md`](superpowers/reports/2026-10-18-ecommerce-v2-core-commerce.md) | `229a62e..8eb0ef3` | 114 |
| v2 P0 review fixes | `superpowers/plans/2026-10-18-phase-a-review-fixes.md` | [`reports/2026-10-18-phase-a-review-fixes.md`](superpowers/reports/2026-10-18-phase-a-review-fixes.md) | `f6e991d..7e149a6` | 130 |
| v2 P1 — email (B1) | `superpowers/plans/2026-10-19-phase-b1-email-flows.md` | [`reports/2026-10-19-phase-b1-email-flows.md`](superpowers/reports/2026-10-19-phase-b1-email-flows.md) | `9011743..ac5b1ea` | 159 |
| v2 P1 — profile (B2) | `superpowers/plans/2026-10-19-phase-b2-profile-address-book.md` | [`reports/2026-10-19-phase-b2-profile-address-book.md`](superpowers/reports/2026-10-19-phase-b2-profile-address-book.md) | `e04ac31..c4afdc6` | 178 |
| v2 P1 — coupon (B3) | `superpowers/plans/2026-10-19-phase-b3-coupons.md` | [`reports/2026-10-19-phase-b3-coupons.md`](superpowers/reports/2026-10-19-phase-b3-coupons.md) | `910b49d..8f89825` | 206 |
| v2 P1 — reviews (B4) | `superpowers/plans/2026-10-22-phase-b4-reviews.md` | [`reports/2026-10-22-phase-b4-reviews.md`](superpowers/reports/2026-10-22-phase-b4-reviews.md) | `865ce74..1f35832` | 241 |
| v2 P1 — shipping & tax (B5) | `superpowers/plans/2026-10-22-phase-b5-shipping-tax.md` | [`reports/2026-10-22-phase-b5-shipping-tax.md`](superpowers/reports/2026-10-22-phase-b5-shipping-tax.md) | `bfaa5f7..46edad9` | 266 |

## Backlog còn lại (ngoài scope v2)
- Background jobs, caching layer, CI/CD pipeline
- Upload file ảnh thật (chờ storage bền vững — Render free dùng disk tạm, file sẽ mất khi redeploy)
- Render cold-start UX: warming/keepalive nếu cần
- Đa tiền tệ (hiện chỉ VND; ngoại tệ chỉ để tham khảo)