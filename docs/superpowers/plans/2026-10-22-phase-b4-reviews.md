# Phase B4 — Reviews & ratings: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task-by-task.

**Goal:** Cho phép khách đã mua đánh giá sản phẩm, admin kiểm duyệt, hiển thị điểm trung bình trên catalog và trang chi tiết.

**Architecture:** Entity `Review` + `ReviewService` (điều kiện mua hàng, kiểm duyệt, aggregate) → `ProductDetailsViewModel` cho trang chi tiết, `ProductListViewModel.Ratings` cho catalog → màn hình admin duyệt/xóa. Tất cả kiểm tra lại ở server.

**Tech Stack:** ASP.NET Core MVC, EF Core 10, SQLite in-memory cho test, xUnit + Moq, Tailwind, notyf.

**Spec:** `docs/superpowers/specs/2026-10-22-phase-b4-reviews-design.md`

## Global Constraints

- .NET 10, build phải `0 Warning(s), 0 Error(s)`.
- Mọi action POST đều có `[ValidateAntiForgeryToken]` và `[EnableRateLimiting("mutating")]`; action admin có `[Authorize(Roles = AppRoles.Admin)]`.
- Thông báo tiếng Việt đúng như spec D-B4-4/5/10.
- Test dùng `Method_Scenario_Expected`, plain `Assert`, `TestDbContextFactory.CreateInMemoryDbContext()`, giữ `SqliteConnection` sống.
- Không đổi hành vi checkout/coupon/order hiện có.

## Review Focus

- User chưa mua hoặc đơn chưa `Completed` gửi POST trực tiếp → phải bị từ chối, không tạo review.
- Hai request đồng thời cùng user/sản phẩm → unique index chặn, trả thông báo trùng thay vì 500.
- Review chưa duyệt không được xuất hiện trong danh sách công khai hoặc điểm trung bình.
- Rating ngoài 1..5 hoặc comment >1000 ký tự gửi thẳng qua POST → từ chối, không lưu.
- Email người đánh giá không bị lộ đầy đủ trên UI công khai.

---

### Task 1: Review entity, service, migration

**Files:**
- Create: `ECommerce.Web/Models/Entities/Review.cs`
- Create: `ECommerce.Web/Services/ReviewSummary.cs`
- Create: `ECommerce.Web/Services/IReviewService.cs`
- Create: `ECommerce.Web/Services/ReviewService.cs`
- Modify: `ECommerce.Web/Data/ApplicationDbContext.cs`
- Modify: `ECommerce.Web/Program.cs`
- Create: `ECommerce.Web/Data/Migrations/*_AddReviews.cs` (dotnet ef)
- Test: `ECommerce.Web.Tests/Services/ReviewServiceTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed record ReviewSummary(decimal AverageRating, int Count);

public interface IReviewService
{
    Task<bool> CanReviewAsync(string userId, int productId);
    Task<bool> HasReviewedAsync(string userId, int productId);
    Task<(bool Success, string Message)> CreateAsync(string userId, int productId, int rating, string? comment);
    Task<IReadOnlyList<Review>> GetApprovedAsync(int productId);
    Task<ReviewSummary> GetSummaryAsync(int productId);
    Task<IReadOnlyDictionary<int, ReviewSummary>> GetSummariesAsync(IEnumerable<int> productIds);
    Task<IReadOnlyList<Review>> GetAllAsync(bool? approved = null);
    Task<(bool Success, string Message)> ApproveAsync(int id);
    Task<(bool Success, string Message)> DeleteAsync(int id);
}
```
- `Review { Id, ProductId, Product, UserId, User, Rating, Comment?, IsApproved, CreatedAt }`.

- [ ] **Step 1: Viết test đỏ cho service** — các fact:
  `CanReview_WhenCompletedOrderContainsProduct_ReturnsTrue`, `CanReview_WhenOrderNotCompleted_ReturnsFalse`, `CanReview_WhenAlreadyReviewed_ReturnsFalse`, `CanReview_WhenUserMissing_ReturnsFalse`, `Create_WhenEligible_AddsUnapprovedReview`, `Create_WhenRatingOutOfRange_ReturnsFalse` (Theory 0,6), `Create_WhenCommentTooLong_ReturnsFalse`, `Create_WhenAlreadyReviewed_ReturnsFalse`, `Create_WhenNotPurchased_ReturnsFalse`, `Create_WhenCommentBlank_StoresNull`, `GetApproved_ReturnsOnlyApprovedNewestFirst`, `GetSummary_ComputesAverageOfApprovedOnly`, `GetSummary_WhenNoReviews_ReturnsZero`, `GetSummaries_GroupsByProduct`, `Approve_SetsIsApproved`, `Approve_WhenMissing_ReturnsFalse`, `Delete_RemovesReview`, `GetAll_WhenPendingFilter_ReturnsUnapproved`.
- [ ] **Step 2: Chạy test xác nhận fail** (`dotnet test --filter ReviewServiceTests`).
- [ ] **Step 3: Implement entity + service + DbContext config + DI.** Unique index `(ProductId, UserId)`; comment max 1000; `Comment` trim/null; `CreateAsync` kiểm tra rating/comment/trùng/điều kiện mua; bắt `DbUpdateException` trả thông báo trùng; aggregate chỉ review `IsApproved`.
- [ ] **Step 4: Tạo migration + cập nhật DB dev** (`dotnet ef migrations add AddReviews`, build lại, `database update --no-build`).
- [ ] **Step 5: Chạy full suite, mutation check** (bỏ điều kiện `Status == Completed` phải làm test fail rồi khôi phục), commit `feat(reviews): add review entity, service and migration`.

---

### Task 2: Product details reviews + submit form

**Files:**
- Create: `ECommerce.Web/Models/ViewModels/ProductDetailsViewModel.cs`
- Create: `ECommerce.Web/Models/ViewModels/ReviewFormViewModel.cs`
- Modify: `ECommerce.Web/Controllers/ProductsController.cs`
- Modify: `ECommerce.Web/Views/Products/Details.cshtml` (đổi `@model`)
- Test: `ECommerce.Web.Tests/Controllers/ProductsControllerReviewTests.cs`

**Interfaces:**
- Consumes: `IReviewService`, `ReviewSummary` từ Task 1.
- Produces: `ProductDetailsViewModel { Product, Reviews, AverageRating, ReviewCount, CanReview, HasReviewed, NewReview }`; `POST /Products/AddReview` nhận `(int id, ReviewFormViewModel model)`.

- [ ] **Step 1: Test đỏ controller**: `Details_WhenEligibleUser_ShowsReviewForm`, `Details_WhenAnonymous_HidesReviewForm`, `Details_ShowsApprovedReviewsAndSummary`, `AddReview_WhenValid_RedirectsAndToastsSuccess`, `AddReview_WhenModelInvalid_ToastsErrorAndDoesNotCallService`, `AddReview_WhenServiceRejects_ToastsError`.
- [ ] **Step 2: Chạy test fail.**
- [ ] **Step 3: Implement ViewModel + controller + view.** `[Authorize]` cho `AddReview`; form gồm chọn sao 1..5, textarea comment max 1000; danh sách review đã duyệt; tên che `local[..3]***`; trạng thái chưa đăng nhập/đã đánh giá/chưa mua.
- [ ] **Step 4: Build + full suite + mutation check** (đảo `CanReview`/bỏ `[Authorize]` phải fail test tương ứng rồi khôi phục), commit `feat(reviews): show reviews and submission form on product details`.

---

### Task 3: Catalog aggregate ratings

**Files:**
- Modify: `ECommerce.Web/Models/ViewModels/ProductListViewModel.cs` (thêm `IReadOnlyDictionary<int, ReviewSummary> Ratings`)
- Modify: `ECommerce.Web/Controllers/ProductsController.cs` (Index nạp summaries)
- Modify: `ECommerce.Web/Views/Products/Index.cshtml` (hiển thị sao + số lượng)
- Test: `ECommerce.Web.Tests/Controllers/ProductsControllerCatalogRatingTests.cs`

- [ ] **Step 1: Test đỏ** `Index_LoadsRatingSummariesForListedProducts`, `Index_WhenNoReviews_LeavesRatingsEmpty`.
- [ ] **Step 2: Chạy fail.**
- [ ] **Step 3: Implement** — gọi `GetSummariesAsync(products.Select(p => p.Id))`; view hiển thị `★ 4,5 (3)` (dùng `ToString("0.0")`, dấu phẩy thập phân theo culture vi-VN) hoặc `Chưa có đánh giá`.
- [ ] **Step 4: Build + full suite + commit** `feat(reviews): show aggregate ratings on product catalog`.

---

### Task 4: Admin moderation + live verify

**Files:**
- Modify: `ECommerce.Web/Controllers/AdminController.cs` (ctor + `Reviews`, `ApproveReview`, `DeleteReview`)
- Create: `ECommerce.Web/Views/Admin/Reviews.cshtml`
- Modify: `ECommerce.Web/Views/Admin/Index.cshtml` (nút "Đánh giá")
- Modify: `ECommerce.Web.Tests/Controllers/AdminAuthorizationTests.cs`
- Test: bổ sung service/controller nếu thiếu

- [ ] **Step 1: Cập nhật ma trận phân quyền trước** (thêm 3 action) — chạy fail vì action chưa tồn tại.
- [ ] **Step 2: Implement admin actions + view.** `Reviews(string? status = null)` map `pending`/`approved`/`all`; nút Duyệt/Xóa có confirm; hiển thị sản phẩm, người đánh giá (che email), sao, nội dung, ngày.
- [ ] **Step 3: Build + full suite.**
- [ ] **Step 4: Live verify** (port 5080, script tự teardown): admin tạo user/đơn? Không đủ thao tác — dùng SQL cập nhật đơn của user live thành `Completed`; user gửi review → thấy "chờ kiểm duyệt", review chưa hiện công khai; admin `/Admin/Reviews` thấy → duyệt → trang sản phẩm hiện review + điểm; user thứ hai chưa mua không thấy form; POST trực tiếp bị từ chối.
- [ ] **Step 5: Graph gate + report** `detect-changes --scope compare --base-ref 8f89825`; report `.superpowers/sdd/2026-10-22-phase-b4-reviews/report-phase-b4-reviews.md`; commit `feat(admin): add review moderation screens`.
