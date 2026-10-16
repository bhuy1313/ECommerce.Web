# E-Commerce Roadmap

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
  - Tích hợp partial `_LoginPartial.cshtml` và hiển thị số lượng giỏ hàng thực tế động (`ICartService`)
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
  - Đảm bảo logic nghiệp vụ và 23/23 unit tests giữ vững 100%

## Phase 8.6 — UI Admin Portal Redesign (TechNova Style)
- [x] Khung Layout Admin: `Views/Shared/_AdminLayout.cshtml` (Sidebar cố định/thu gọn, Topbar, Tailwind CSS, FontAwesome 6, Inter font, Flash alerts)
- [x] Trang Dashboard Tổng quan: `Views/Admin/Index.cshtml` (KPI cards, phân tích trạng thái đơn, bảng cảnh báo low-stock, bảng đơn hàng gần đây)
- [x] Quản lý Sản phẩm: `Views/Admin/Products.cshtml` & `Views/Admin/ProductForm.cshtml` (Lọc theo danh mục, trạng thái active, form thêm/sửa sản phẩm)
- [x] Quản lý Danh mục: `Views/Admin/Categories.cshtml` & `Views/Admin/CategoryForm.cshtml` (Bảng danh mục, số lượng sp, form thêm/sửa danh mục)
- [x] Quản lý Đơn hàng: `Views/Admin/Orders.cshtml` & `Views/Admin/OrderDetails.cshtml` (Tabs phân loại trạng thái, bảng đơn, cập nhật trạng thái đơn hàng)
- [x] Quản lý Người dùng: `Views/Admin/Users.cshtml` & `Views/Admin/EditUserRoles.cshtml` (Danh sách user, số đơn, form phân quyền role an toàn)
- [x] Kiểm thử & Verification: `dotnet build` và chạy lại 23/23 unit tests

## Phase 9 — Deploy (Tạm đóng băng / Freeze)
- [ ] Chốt hosting (Azure / IIS / Docker) — Tạm dừng
- [ ] Migration production
- [ ] Cấu hình appsettings.Production, HSTS, HTTPS
- [ ] Kiểm thử trên môi trường public

## Backlog (ngoài scope v1 — theo Non-Goals trong doc)
- Payment gateway, product images, coupon/giảm giá, reviews, reporting nâng cao,
  audit log, background jobs, caching, CI/CD