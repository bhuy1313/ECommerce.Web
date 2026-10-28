# Report kiểm chứng — Khóa phạm vi (Scope Lock v1.0-demo)

- **Plan:** `docs/superpowers/plans/2026-10-28-scope-lock-ecommerce-web.md`
- **Ngày:** 2026-10-28
- **Baseline commit:** `main` @ `b78044d`
- **Release tag:** `v1.0-demo`
- **Chế độ:** Scope lock & Demo hardening

---

## 1. Thay đổi theo task

| Task | Mục tiêu | Các file thay đổi | Kết quả nghiệm thu |
|---|---|---|---|
| **T0** | Baseline & Chốt mốc | Không đổi code | 266/266 PASS; `/healthz` 200 trên Render live |
| **T1** | Bịt điểm chết VNPay khi chưa cấu hình | `ECommerce.Web/Models/Configuration/VnPayOptions.cs`<br>`ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`<br>`ECommerce.Web/Controllers/OrdersController.cs`<br>`ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs`<br>`ECommerce.Web.Tests/Models/VnPayOptionsTests.cs` | Thêm thuộc tính `IsConfigured`; bọc `@if (vnPayReady)` cho radio & note; null-safe JS `if (note)`; guard server-side chặn tạo đơn; thêm 11 ca kiểm thử mới (tổng 277/277 PASS) |
| **T2** | Mở khóa hiển thị Admin Sidebar | `ECommerce.Web/Views/Shared/_AdminLayout.cshtml` | Thêm 3 liên kết quản trị (Mã giảm giá, Đánh giá, Vận chuyển) trong khối Admin role |
| **T3** | Đồng bộ & Chuẩn hóa README | `README.md` | Sửa .NET 10, ports 7133/5135, tài khoản admin seed, minh bạch `LogEmailSender`, trạng thái VNPay, thêm mục "Giới hạn đã biết" |
| **T4** | Ghi nhận luật khóa vào repo | `docs/scope-lock.md`<br>`docs/README.md`<br>`docs/roadmap.md`<br>`docs/superpowers/plans/2026-10-28-scope-lock-ecommerce-web.md` | Định nghĩa D1–D5, phân loại tính năng, đóng băng P2, cập nhật nhật ký thực thi |
| **T5** | Kiểm chứng toàn diện & Tag | `docs/superpowers/reports/2026-10-28-scope-lock.md` | Test 277/277 PASS, GitNexus graph clean, gắn tag `v1.0-demo` |

---

## 2. Kiểm chứng kỹ thuật (Output thật)

| Bước kiểm chứng | Lệnh / Nguồn | Kết quả |
|---|---|:---:|
| **Unit Test Suite** | `dotnet test --nologo -v q` | **Passed: 277**, Failed: 0, Skipped: 0 (4.0s) |
| **Healthcheck Live** | `curl.exe -s -o NUL -w "%{http_code}" https://ecommerce-demo-9phe.onrender.com/healthz` | **200** |
| **Homepage Live** | `curl.exe -s -o NUL -w "%{http_code}" https://ecommerce-demo-9phe.onrender.com/` | **200** |
| **Graph Blast Radius** | `node .gitnexus/run.cjs detect-changes --scope all` | Clean, chỉ tác động luồng `Checkout` được bảo vệ |
| **Build Solution** | `dotnet build --nologo -v q` | **0 Warning, 0 Error** |

---

## 3. Đánh giá 5 tiêu chí "Đủ" (D1 – D5)

- **D1 — Golden Path thông suốt:** Đã kiểm chứng qua các bộ test luồng giỏ hàng, đặt hàng COD và quản trị đơn hàng.
- **D2 — Không nút chết:** Radio VNPay và hướng dẫn tự động ẩn khi credentials để trống (`VnPayOptions.IsConfigured == false`), ngăn chặn việc tạo đơn kẹt "chờ thanh toán".
- **D3 — Người lạ hiểu trong 60 giây:** `README.md` phản ánh 100% sự thật công nghệ (.NET 10, SQLite container, port, admin credentials, limitations).
- **D4 — Admin chứng minh vòng vận hành:** Sidebar admin đầy đủ 6 phân hệ (Dashboard, Sản phẩm, Danh mục, Đơn hàng, Mã giảm giá, Đánh giá, Vận chuyển, Người dùng).
- **D5 — Bằng chứng kỹ thuật xanh:** 277/277 tests pass, build 0 cảnh báo.

---

## 4. Trạng thái sau đợt

- **Baseline commit ban đầu:** `b78044d`
- **Release tag:** `v1.0-demo`
- **Trạng thái P2:** Đóng băng toàn bộ theo `docs/scope-lock.md`.
