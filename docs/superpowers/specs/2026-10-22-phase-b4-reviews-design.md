# Phase B4 — Reviews & ratings (thiết kế)

**Ngày:** 2026-10-22
**Phạm vi:** đánh giá sản phẩm có kiểm duyệt, chỉ khách đã mua (đơn `Completed`) mới được đánh giá; hiển thị điểm trung bình trên catalog.

## Quyết định thiết kế

- **D-B4-1 — Entity `Review`:** `Id`, `ProductId`, `UserId`, `Rating` (int 1..5), `Comment` (string?, tối đa 1000), `IsApproved` (bool, mặc định `false`), `CreatedAt` (UTC), `Product`, `User`. Unique index `(ProductId, UserId)` — mỗi người chỉ đánh giá một sản phẩm một lần, không sửa/xóa (admin xóa được).
- **D-B4-2 — Điều kiện đánh giá:** user phải có ít nhất một đơn `Status == Completed` chứa `OrderItem.ProductId` tương ứng. `Pending/Processing/Shipped/Cancelled` không đủ điều kiện. Server kiểm tra lại khi POST; không tin trạng thái UI.
- **D-B4-3 — Kiểm duyệt:** review mới luôn `IsApproved = false`; chỉ review đã duyệt mới hiển thị công khai và được tính điểm trung bình. Admin duyệt hoặc xóa trong `/Admin/Reviews` (mặc định lọc chờ duyệt).
- **D-B4-4 — Một review/người/sản phẩm:** nếu đã có (kể cả chưa duyệt) thì không cho gửi lần hai; thông báo `"Bạn đã đánh giá sản phẩm này."`. Unique index là chốt chặn race; `DbUpdateException` trùng khóa trả cùng thông báo.
- **D-B4-5 — Nội dung:** `Rating` bắt buộc 1..5; `Comment` tùy chọn, trim, tối đa 1000 ký tự; rỗng → `null`. Rating ngoài khoảng hoặc comment quá dài bị từ chối với thông báo tiếng Việt.
- **D-B4-6 — Tên hiển thị:** che email: lấy phần trước `@`, giữ tối đa 3 ký tự + `***` (ví dụ `qa***`); không có email → `"Khách hàng"`. Không hiển thị email đầy đủ.
- **D-B4-7 — Trang chi tiết sản phẩm:** dùng `ProductDetailsViewModel` gồm `Product`, `Reviews` (đã duyệt, mới nhất trước), `AverageRating`, `ReviewCount`, `CanReview`, `HasReviewed`, `NewReview` (`ReviewFormViewModel`). Khách chưa đăng nhập thấy lời mời đăng nhập; đã mua + chưa đánh giá thấy form; đã đánh giá thấy thông báo; chưa mua thấy giải thích.
- **D-B4-8 — Catalog:** `Products/Index` hiển thị `★ trung bình (số lượng)` cạnh tên sản phẩm; chỉ tính review đã duyệt; chưa có → `"Chưa có đánh giá"`. Điểm trung bình làm tròn 1 chữ số thập phân (`AwayFromZero`).
- **D-B4-9 — Admin:** `GET /Admin/Reviews?status=pending|approved|all` (mặc định `pending`), `POST /Admin/ApproveReview/{id}`, `POST /Admin/DeleteReview/{id}`; tất cả `[Authorize(Roles = AppRoles.Admin)]` + anti-forgery. Thêm nút "Đánh giá" ở `/Admin`.
- **D-B4-10 — Thông báo:** gửi thành công `"Cảm ơn bạn! Đánh giá đang chờ kiểm duyệt."`; duyệt `"Đã duyệt đánh giá."`; xóa `"Đã xóa đánh giá."`; không tìm thấy `"Không tìm thấy đánh giá."`.
- **D-B4-11 — Service API:** `IReviewService` cung cấp `CanReviewAsync`, `HasReviewedAsync`, `CreateAsync`, `GetApprovedAsync`, `GetSummaryAsync`, `GetSummariesAsync`, `GetAllAsync`, `ApproveAsync`, `DeleteAsync`.
- **D-B4-12 — Xóa sản phẩm:** `Review.ProductId` FK cascade; xóa user (Identity) cascade theo `UserId`. Xóa review không ảnh hưởng đơn hàng.

## Ngoài phạm vi

- Sửa review, trả lời của shop, upload ảnh, vote hữu ích, phân trang review.
- Gửi email nhắc đánh giá sau khi đơn hoàn tất.
- Điểm trung bình trên trang chủ (chỉ catalog + chi tiết sản phẩm).
