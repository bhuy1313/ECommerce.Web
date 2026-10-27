# Report - Phase B4: Reviews & ratings

**Ngày:** 2026-10-22
**Plan:** `docs/superpowers/plans/2026-10-22-phase-b4-reviews.md`
**Spec:** `docs/superpowers/specs/2026-10-22-phase-b4-reviews-design.md`

## Kết quả

- Commit: `865ce74` (entity + service + migration), `b2fe0da` (chi tiết sản phẩm + form), `7472bb9` (điểm trung bình catalog), `1f35832` (admin kiểm duyệt).
- Baseline trước: 206 test. Sau: **241/241 test pass** (+35).
- Build `dotnet build ECommerce.Web.slnx --no-incremental`: **0 warning, 0 error**.
- Migration `20261022072726_AddReviews` đã áp vào dev LocalDB: bảng `Reviews`, unique `(ProductId, UserId)`, index `IsApproved`, FK cascade Product/User.

## Thay đổi chính

- `Review` + `ReviewService`: chỉ user có đơn `Completed` chứa sản phẩm mới được đánh giá; một review/người/sản phẩm; rating 1..5; comment tối đa 1000; review mới chờ duyệt; unique index chặn race.
- Trang chi tiết sản phẩm: điểm trung bình, danh sách review đã duyệt (tên che `abc***`), form gửi đánh giá, trạng thái chưa đăng nhập/đã đánh giá/chưa mua.
- Catalog: `★ 5.0 (n đánh giá)` cạnh tên sản phẩm, chỉ tính review đã duyệt.
- Admin `/Admin/Reviews?status=pending|approved|all`: duyệt/xóa, nút truy cập từ dashboard.

## Live verify (một lần boot, teardown sạch)

User `b4.1.1792654734@technova.local` mua đơn `20014`, đơn được chuyển `Completed`, gửi review 5 sao; **21/21 CHECK = True, `FAILED_CHECKS=0`**:

```
buyer1_register_ok, buyer1_add_to_cart, buyer1_order_created,
eligible_shows_review_form, review_submitted, pending_review_hidden_from_public,
pending_review_saved, owner_sees_reviewed_notice, buyer2_register_ok,
non_buyer_has_no_form, forged_review_rejected, admin_login_ok,
admin_pending_list_ok, admin_pending_review_id_found, admin_approve_ok,
approved_review_visible, approved_review_db, catalog_shows_rating,
admin_delete_ok, deleted_review_hidden, teardown_clean
```

Đã kiểm tra: review chờ duyệt không xuất hiện công khai; user chưa mua POST trực tiếp bị từ chối; admin duyệt → hiện review + `5.0 (1 đánh giá)` trên catalog; admin xóa → review biến mất. Port 5080 không còn listen sau khi chạy.

## Side effect dev DB

- Order `20014` (COD, note `B4 live ...`, đã set `Completed` bằng SQL để test điều kiện đánh giá).
- Users `b4.1.1792654734@`, `b4.2.1792654734@`, mật khẩu `Qa!23456`.
- Bảng `Reviews` còn 0 dòng (review test đã bị admin xóa trong live verify).

## Graph gate

`node .gitnexus/run.cjs detect-changes --scope compare --base-ref 8f89825`

- 24 files / 159 symbols / 7 execution flows, **risk high**, không `partial`/`truncated`.
- Index rebuild trước gate: 2.010 nodes / 4.442 edges.

## Ghi chú

- `ApplicationUser`/`Product` không thêm collection ngược (chỉ navigation một chiều trên `Review`) để tránh đụng entity CRITICAL.
- `ReviewService` dùng `Include(User)` nên test phải seed user cho review của người khác (tránh INNER JOIN do FK non-null).
- Ma trận phân quyền admin thêm 3 action: `Reviews`, `ApproveReview`, `DeleteReview`.
