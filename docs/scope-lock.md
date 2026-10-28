# Quy tắc khóa phạm vi (Scope Lock) — ECommerce.Web

| Trường | Giá trị |
|---|---|
| **Ngày chốt** | 2026-10-28 |
| **Mục đích** | Đóng băng phạm vi phát triển, bịt các điểm chết luồng demo cho CV/Portfolio |
| **Baseline commit** | `main` @ `b78044d` |
| **Tag phát hành** | `v1.0-demo` |
| **Nguyên tắc cốt lõi** | Không thêm bảng/entity mới, không thêm package mới, bảo vệ luồng Golden Path bằng mọi giá |

---

## 1. Định nghĩa "Đủ" cho bản Demo (D1 – D5)

Dự án này là **portfolio piece** chứng minh năng lực thiết kế kiến trúc và triển khai luồng thương mại điện tử trọn vẹn, không hướng tới việc chạy quy mô sàn Shopee thật. Chất lượng được đo bằng 5 điều kiện kiểm chứng được:

| # | Tiêu chí | Mô tả kiểm chứng | Trạng thái |
|---|---|---|:---:|
| **D1** | **Golden Path thông suốt** | Toàn bộ 5 bước từ xem hàng đến đặt hàng và quản trị chạy trọn vẹn, không có điểm chết hoặc crash | ✅ Đạt |
| **D2** | **Không nút chết** | Mọi control hiển thị đều hoạt động. Tính năng chưa cấu hình (ví dụ cổng thanh toán online) bắt buộc phải ẩn | ✅ Đạt |
| **D3** | **Tài liệu trung thực** | README và trang chủ mô tả đúng 100% hiện trạng (.NET 10, ports, tài khoản seed, log email) | ✅ Đạt |
| **D4** | **Chứng minh vòng vận hành** | Admin đăng nhập, xem đơn hàng, chuyển trạng thái đơn, thêm sản phẩm, duyệt đánh giá | ✅ Đạt |
| **D5** | **Bằng chứng kỹ thuật xanh** | Toàn bộ test suite vượt qua (277/277 tests), endpoint `/healthz` trả HTTP 200 | ✅ Đạt |

---

## 2. Phân loại tính năng (4 nhóm)

### 2.1 CORE (Cốt lõi — Cấm đụng làm vỡ)
- Xác thực tài khoản: Đăng ký, đăng nhập, bảo vệ brute-force (Identity Lockout 5 lần sai / 5 phút).
- Danh mục sản phẩm & Trang chủ: Tìm kiếm cơ bản, chi tiết sản phẩm.
- Giỏ hàng: Thêm, sửa số lượng, xóa sản phẩm.
- Quy trình Checkout: Địa chỉ giao hàng, sổ địa chỉ, tính phí vận chuyển, áp mã giảm giá, tính thuế VAT.
- Thanh toán COD: Luồng thanh toán mặc định luôn đảm bảo thành công.
- Lịch sử đơn hàng: Xem danh sách, chi tiết đơn hàng, hủy đơn theo quy tắc tồn kho.
- Phía quản trị: Quản lý sản phẩm, danh mục, đơn hàng.

### 2.2 BUILT-FROZEN (Đã hoàn thành — Đóng băng, không mở rộng)
- **VNPay Integration**: Giữ nguyên code tích hợp (HMAC-SHA512 checksum, IPN callback, idempotency), nhưng ẩn radio ở UI khi thiếu credentials để không tạo đơn rác.
- **Mã giảm giá (Coupon)**: Giữ nguyên cơ chế giảm theo % và giảm tiền cố định. Không thêm loại coupon phức tạp (như mua 1 tặng 1, tích lũy điểm).
- **Đánh giá (Review)**: Giữ nguyên điều kiện chỉ cho phép người đã mua hàng (verified purchase) đánh giá và admin duyệt. Không thêm tính năng trả lời review hay upvote.
- **Vận chuyển & Thuế**: Giữ bảng cấu hình hiện tại. Không tích hợp API của đơn vị vận chuyển bên thứ 3 (GHN/GHTK).
- **Email Service**: Giữ `LogEmailSender` ghi log; không cấu hình SMTP ra ngoài.
- **Admin**: Giữ các trang quản lý Categories, Users/Roles, Coupons, Reviews, ShippingMethods.

### 2.3 HIDE-OR-FIX (Điểm chết — Đã khắc phục)
1. **VNPay thiếu config**: Đã ẩn lựa chọn radio VNPay trên form checkout khi `VnPayOptions.IsConfigured == false` và thêm guard chặn POST phía server.
2. **Admin Sidebar**: Đã thêm 3 liên kết quản trị (Mã giảm giá, Đánh giá, Vận chuyển) vào menu sidebar.
3. **README**: Đã sửa các thông tin lệch lạc về phiên bản .NET, port, tài khoản admin và bổ sung giới hạn đã biết.

### 2.4 NEVER (Khóa vĩnh viễn — Không làm trong repo này)
- **5 hạng mục P2 trong roadmap**:
  - Catalog nâng cao (sort theo giá/ngày, lọc khoảng giá, phân trang đa tiêu chí)
  - Danh sách yêu thích (Wishlist)
  - Xuất báo cáo doanh thu Excel/PDF
  - Audit log toàn diện thao tác admin
  - Hệ thống gợi ý sản phẩm liên quan
- **Hạ tầng ngoài scope**: Ví dụ MoMo/Stripe, SMTP thật, Cloudinary/S3 storage thật, Hangfire/background jobs, Redis cache, CI/CD phức tạp, đa ngôn ngữ đầy đủ.

---

## 3. Luồng mua hàng chuẩn (Golden Path)

Mọi thay đổi trên codebase trước khi commit hoặc release bắt buộc phải kiểm chứng qua 5 bước sau:

1. **Khách hàng**: Truy cập trang chủ ➔ tìm/chọn danh mục ➔ xem chi tiết sản phẩm.
2. **Khách hàng**: Thêm 2 sản phẩm vào giỏ hàng ➔ cập nhật số lượng trong giỏ.
3. **Khách hàng**: Đến trang Checkout ➔ điền địa chỉ giao hàng (hoặc chọn từ sổ địa chỉ) ➔ áp mã giảm giá ➔ chọn phương thức vận chuyển ➔ chọn **COD** ➔ Đặt hàng.
4. **Khách hàng**: Xem trang Lịch sử đơn hàng ➔ đơn hàng mới hiển thị chính xác với trạng thái `Pending`.
5. **Admin**: Đăng nhập quyền Admin ➔ vào Quản lý Đơn hàng ➔ chuyển trạng thái đơn (Processing / Shipped) ➔ duyệt 1 đánh giá mới ➔ tạo/sửa 1 sản phẩm.

**Tiêu chí thành công:** Không xuất hiện lỗi HTTP 500, không văng lỗi JavaScript console, không hiển thị thông báo lỗi thiếu cấu hình.

---

## 4. Quy tắc khóa cứng (Hard Lock Rules)

### Các thao tác được phép:
- Sửa lỗi (bug fix) có bằng chứng tái hiện rõ ràng.
- Vá lỗi hiển thị (ẩn các tính năng chưa hoàn thiện/chưa cấu hình).
- Cập nhật tài liệu, screenshot, copy văn bản.
- Vá lỗ hổng bảo mật cấp bách qua cập nhật phiên bản thư viện.

### Nghiêm cấm:
- ❌ Không thêm entity/bảng cơ sở dữ liệu mới.
- ❌ Không tạo Controller, Area, hoặc trang Admin mới.
- ❌ Không cài thêm NuGet packages mới (trừ security patch).
- ❌ Không sửa đổi query cơ bản của Catalog / Trang chủ.
- ❌ Không làm các tính năng P2 trong nhánh chính (`main`).

### Tiêu chuẩn "Hoàn thành" cho mỗi thay đổi:
```bash
dotnet test                                          # 277/277 PASS
# Chạy kiểm chứng 5 bước Golden Path
node .gitnexus/run.cjs detect-changes --scope all     # Không có rủi ro ngoài kiểm soát
```
