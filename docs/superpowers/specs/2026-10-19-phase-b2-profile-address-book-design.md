# Phase B2 — Hồ sơ & sổ địa chỉ (design)

**Ngày:** 2026-10-19
**Phạm vi:** roadmap `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` PHASE B @636 (B2).
**Trạng thái:** đã chốt hướng với user (bỏ qua phiên hỏi brainstorming).

## Mục tiêu

Người dùng đã đăng nhập có thể lưu nhiều địa chỉ giao hàng, chọn một địa chỉ mặc định, và
dùng lại địa chỉ đã lưu khi thanh toán mà không phải gõ lại.

## Quyết định thiết kế

- **D-B2-1 — Entity `Address` thuộc user.** `Id`, `UserId` (FK `ApplicationUser`, cascade),
  `RecipientName` (100), `RecipientPhone` (15), `AddressLine` (300), `IsDefault` (bool).
  Thêm `ApplicationUser.Addresses`. Đơn hàng vẫn giữ snapshot địa chỉ riêng
  (`Order.RecipientName/RecipientPhone/ShippingAddress`) — không tham chiếu `Address`,
  để sửa/xóa địa chỉ không làm thay đổi đơn cũ.
- **D-B2-2 — `ProfileController` riêng (`/Profile`, `[Authorize]`).** Không nhét vào
  `AccountController` để tránh đổi constructor và làm hỏng 12 test account hiện có.
- **D-B2-3 — `IAddressService` là nơi duy nhất đọc/ghi địa chỉ.** Mọi thao tác theo `id`
  đều kiểm tra `UserId` của địa chỉ; id không thuộc user hoặc không tồn tại trả
  `(false, "Không tìm thấy địa chỉ.")` — controller hiển thị toast lỗi và redirect,
  không trả 404 (không lộ sự tồn tại của bản ghi).
- **D-B2-4 — Đúng một địa chỉ mặc định.** Địa chỉ đầu tiên tự thành mặc định; đặt mặc định
  sẽ bỏ cờ của các địa chỉ khác; xóa địa chỉ mặc định sẽ thăng cấp địa chỉ có `Id` nhỏ nhất
  còn lại.
- **D-B2-5 — Checkout prefill từ địa chỉ mặc định ở server**, và có dropdown chọn địa chỉ
  khác. `SavedAddressId` chỉ là tiện ích UI (JS đổ dữ liệu vào các input); server KHÔNG tin
  id này — đơn hàng luôn dùng các trường `RecipientName/RecipientPhone/ShippingAddress`
  đã post và validate như hiện tại.
- **D-B2-6 — Không giới hạn số địa chỉ** (YAGNI). Validation trùng với checkout.
- **D-B2-7 — Link "Hồ sơ & địa chỉ"** trong menu tài khoản (`_LoginPartial.cshtml`).
- **D-B2-8 — Không thêm NuGet package, không đổi stack.** EF migration mới
  `AddAddresses`; `MigrateAsync()` sẵn có trong `Program.cs` tự áp dụng.

## Luồng

1. `/Profile` (GET): danh sách địa chỉ (mặc định trước), form thêm địa chỉ mới.
2. `/Profile/Add` (POST): validate → service thêm → toast → redirect `/Profile`.
   Lỗi validation: render lại Index, giữ dữ liệu đã nhập.
3. `/Profile/SetDefault` (POST id) và `/Profile/Delete` (POST id): toast + redirect `/Profile`.
4. `/Cart/Checkout` (GET): nếu có địa chỉ mặc định thì điền sẵn 3 trường giao hàng; dropdown
   liệt kê toàn bộ địa chỉ đã lưu. Chọn địa chỉ khác → JS điền 3 trường. Người dùng vẫn sửa
   tay được trước khi đặt.
5. `/Orders/Checkout` (POST): không đổi hành vi.

## Kiểm thử

- Unit service (SQLite in-memory): mặc định đầu tiên, đặt mặc định, xóa mặc định thăng cấp,
  chặn id của user khác, chỉ trả địa chỉ của mình.
- Controller `ProfileController`: add hợp lệ/lỗi validation/không sở hữu; set default; delete.
- `CartController` checkout: prefill từ mặc định, expose options, không có địa chỉ thì để trống.
- Live verify: thêm 2 địa chỉ → đổi mặc định → checkout prefill đúng → đặt COD → xóa mặc định
  → địa chỉ còn lại thành mặc định; dọn dev server.

## Ngoài phạm vi

Sửa địa chỉ (chỉ thêm/xóa/đặt mặc định), địa chỉ cấp quốc gia/tỉnh-phường có cấu trúc,
tự động lưu địa chỉ mới từ checkout, chọn địa chỉ trong trang admin.
