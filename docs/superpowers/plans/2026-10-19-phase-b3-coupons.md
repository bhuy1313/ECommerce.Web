# Phase B3 — Coupon / mã giảm giá (implementation plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin quản lý mã giảm giá; khách áp mã ở checkout và server trừ lượt dùng atomic trong transaction tạo đơn.
**Architecture:** `Coupon` entity + `ICouponService` (validate read-only cho preview, redeem atomic bằng `ExecuteUpdateAsync` trong transaction sẵn có của `OrderService`); `Order` lưu snapshot `DiscountAmount`/`CouponCode`; preview dùng GET `?couponCode=`, POST mang mã bằng hidden input.
**Tech Stack:** ASP.NET Core MVC, EF Core 10 (dev LocalDB / test SQLite in-memory), xUnit + Moq, Tailwind CDN, notyf.
**Spec:** `docs/superpowers/specs/2026-10-19-phase-b3-coupons-design.md`

## Global Constraints

- Không thêm NuGet package; không đổi stack. .NET 10, EF Core 10.
- Tiền VND: làm tròn 0 chữ số thập phân (`MidpointRounding.AwayFromZero`), giảm không vượt subtotal.
- Một mã mỗi đơn; `UsageLimit` là tổng toàn hệ thống (không per-user).
- Thông báo/validation tiếng Việt, dùng đúng câu trong spec D-B3-8.
- TDD: test đỏ trước; RED do compile-time phải mutation check rồi reverse-edit (không `git checkout`).
- Build `0 Warning(s), 0 Error(s)`; commit chỉ đường dẫn cụ thể; không stage `docs/roadmap.md`, `opencode.json`, csproj nhiễu EOL.
- `ExpiresAt` theo ngày UTC, inclusive (`EndsOn`); ngày bắt đầu/kết thúc là `DateOnly`.

## Review Focus

- Mã trống/khoảng trắng = không dùng mã (không lỗi, không giảm).
- Hai checkout đồng thời với `UsageLimit = 1`: chỉ một đơn thành công; đơn kia rollback, kho/giỏ nguyên.
- Mã hợp lệ ở preview nhưng hết hạn/hết lượt/bị tắt ở POST: đơn hủy, giỏ còn nguyên, có thông báo.
- Giảm cố định > subtotal: tổng không âm, giảm tối đa bằng subtotal.
- Admin tạo mã trùng (khác hoa/thường), ký tự lạ, phần trăm > 100, ngày kết thúc trước bắt đầu: bị từ chối, không ghi DB.

---

### Task 1: Coupon entity + ICouponService + migration

**Files:**
- Create: `ECommerce.Web/Models/Entities/Enums/DiscountType.cs`
- Create: `ECommerce.Web/Models/Entities/Coupon.cs`
- Create: `ECommerce.Web/Models/ViewModels/CouponFormViewModel.cs`
- Create: `ECommerce.Web/Services/CouponValidationResult.cs`
- Create: `ECommerce.Web/Services/ICouponService.cs`, `ECommerce.Web/Services/CouponService.cs`
- Modify: `ECommerce.Web/Data/ApplicationDbContext.cs`, `ECommerce.Web/Program.cs`
- Create: `ECommerce.Web/Data/Migrations/*_AddCoupons.cs` (generated)
- Test: `ECommerce.Web.Tests/Services/CouponServiceTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum DiscountType { Percentage = 1, FixedAmount = 2 }

public sealed record CouponValidationResult(
    bool IsValid, string? Error, string? Code, decimal DiscountAmount, string? Description);

public interface ICouponService
{
    Task<CouponValidationResult> ValidateAsync(string? code, decimal subtotal);
    Task<CouponValidationResult> RedeemAsync(string? code, decimal subtotal);
    Task<IReadOnlyList<Coupon>> GetAllAsync();
    Task<Coupon?> GetByIdAsync(int id);
    Task<(bool Success, string Message)> CreateAsync(CouponFormViewModel model);
    Task<(bool Success, string Message)> UpdateAsync(CouponFormViewModel model);
    Task<(bool Success, string Message)> DeleteAsync(int id);
    Task<(bool Success, string Message)> ToggleActiveAsync(int id);
}
```
- Blank code: `ValidateAsync`/`RedeemAsync` trả `new CouponValidationResult(false, null, null, 0m, null)`.
- Hợp lệ: `IsValid = true`, `Code` đã chuẩn hóa, `DiscountAmount`, `Description`.

- [ ] **Step 1: Viết test đỏ**

`CouponServiceTests.cs` — helper:

```csharp
private static async Task<(ApplicationDbContext Db, SqliteConnection Conn, CouponService Service)> CreateAsync()
{
    var (db, conn) = TestDbContextFactory.CreateInMemoryDbContext();
    db.Coupons.Add(new Coupon
    {
        Code = "SAVE10", DiscountType = DiscountType.Percentage, Value = 10m,
        MinOrderAmount = 0m, UsageLimit = 5, UsedCount = 0,
        StartsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        EndsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), IsActive = true
    });
    await db.SaveChangesAsync();
    return (db, conn, new CouponService(db));
}
```

Test (16 `[Fact]`, tên + assertion chính):

1. `ValidateAsync_BlankCode_NoDiscountNoError` — `"  "` → `IsValid == false`, `Error == null`, `DiscountAmount == 0`.
2. `ValidateAsync_Percentage_ComputesRoundedDiscount` — subtotal `1_234_567m` → `DiscountAmount == 123_457m`, `IsValid`.
3. `ValidateAsync_Fixed_CapsAtSubtotal` — coupon FixedAmount 500000, subtotal 300000 → 300000.
4. `ValidateAsync_WhenInactive_ReturnsError` — set `IsActive = false` → `IsValid == false`, `Error` chứa "không tồn tại hoặc đã bị vô hiệu hóa".
5. `ValidateAsync_WhenNotStarted_ReturnsError` — `StartsOn = today.AddDays(1)` → Error "chưa có hiệu lực".
6. `ValidateAsync_WhenExpired_ReturnsError` — `EndsOn = today.AddDays(-1)` → Error "đã hết hạn".
7. `ValidateAsync_WhenUsageExhausted_ReturnsError` — `UsedCount = UsageLimit` → Error "đã được sử dụng hết".
8. `ValidateAsync_WhenBelowMinOrder_ReturnsError` — `MinOrderAmount = 1_000_000`, subtotal 999_999 → Error chứa "chưa đạt giá trị tối thiểu".
9. `ValidateAsync_NormalizesCaseAndWhitespace` — `" save10 "` → `IsValid`, `Code == "SAVE10"`.
10. `RedeemAsync_Valid_IncrementsUsedCount` — redeem "save10" → `IsValid`, DB `UsedCount == 1`.
11. `RedeemAsync_WhenLimitReached_SecondCallFails` — coupon `UsageLimit = 1`; gọi redeem 2 lần → lần 2 `IsValid == false`, `Error` chứa "đã được sử dụng hết", DB `UsedCount == 1`.
12. `CreateAsync_NormalizesCodeAndPersists` — VM Code `" new10 "`, Percentage 15, dates today/today+7, limit 3 → success, DB code `NEW10`.
13. `CreateAsync_WhenDuplicateCaseInsensitive_Rejects` — tạo `"save10"` khi đã có `SAVE10` → fail, message "Mã giảm giá đã tồn tại.", count không đổi.
14. `UpdateAsync_ChangesFieldsAndRejectsDuplicate` — đổi value/limit thành công; đổi Code sang code đã tồn tại → fail.
15. `ToggleActiveAsync_FlipsStatus` — true→false, DB đổi.
16. `DeleteAsync_RemovesCoupon` — xóa → success, `FindAsync` null.

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~CouponServiceTests"`
Expected: RED (compile-time — chưa có type).

- [ ] **Step 2: Tạo model**

`Coupon.cs`:
```csharp
public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsActive { get; set; }
}
```
`CouponFormViewModel` (DataAnnotations + `IValidatableObject`):
- `Id` (int), `Code` required/3..50/regex `^[A-Za-z0-9_-]+$`, `DiscountType`,
  `Value` `[Range(0.01, 1_000_000_000)]`, `MinOrderAmount` `[Range(0, 1_000_000_000)]`,
  `UsageLimit` `[Range(1, 1_000_000)]`, `StartsOn`/`EndsOn` `DateOnly?` + `[Required]`,
  `IsActive` (bool, mặc định true).
- `IValidatableObject`: Percentage và `Value > 100` → "Phần trăm giảm phải từ 1 đến 100.";
  `EndsOn < StartsOn` → "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu."

`ApplicationDbContext`: `DbSet<Coupon> Coupons`; config `Code` max 50 + unique index,
`Value`/`MinOrderAmount` precision (18,2).

- [ ] **Step 3: Viết CouponService**

- `Normalize(string code) => code.Trim().ToUpperInvariant()`.
- `ValidateAsync`: blank → blank result; load `AsNoTracking` theo Code chuẩn hóa; kiểm tra theo thứ tự
  tồn tại+active → `DateOnly.FromDateTime(DateTime.UtcNow)` so StartsOn/EndsOn (inclusive) →
  `UsedCount >= UsageLimit` → `subtotal < MinOrderAmount`; tính giảm:
  Percentage `Math.Round(subtotal * Value / 100m, 0, MidpointRounding.AwayFromZero)`,
  FixedAmount `Value`; `Math.Min(discount, subtotal)`; `Math.Max(discount, 0)`;
  `Description`: `$"Giảm {Value:N0}%"` hoặc `$"Giảm {Value:N0} ₫"`.
- `RedeemAsync`: giống validate nhưng sau khi tính giảm, cập nhật atomic:
```csharp
var affected = await _db.Coupons
    .Where(c => c.Id == coupon.Id && c.IsActive
        && c.UsedCount < c.UsageLimit
        && c.StartsOn <= today && c.EndsOn >= today
        && c.MinOrderAmount <= subtotal)
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1));
```
`affected == 0` → `(false, "Mã giảm giá đã được sử dụng hết.", null, 0m, null)`.
- CRUD: create/update normalize + duplicate check (`AnyAsync(c => c.Code == code && c.Id != id)`),
  map VM → entity; messages: "Đã tạo mã giảm giá.", "Đã cập nhật mã giảm giá.",
  "Đã xóa mã giảm giá.", "Mã giảm giá không tồn tại.", "Mã giảm giá đã tồn tại.".
  Toggle message: "Đã cập nhật trạng thái mã giảm giá.".
- `GetAllAsync`: `OrderByDescending(c => c.IsActive).ThenBy(c => c.EndsOn).ThenBy(c => c.Code)`.

- [ ] **Step 4: DI + migration**

`Program.cs` sau `IAddressService`: `builder.Services.AddScoped<ICouponService, CouponService>();`

Run:
`dotnet ef migrations add AddCoupons --project ECommerce.Web --startup-project ECommerce.Web --output-dir Data/Migrations`

Kiểm tra generated: bảng `Coupons` + unique index `IX_Coupons_Code`, precision `decimal(18,2)`.
(Chưa thêm cột Orders ở task này — Task 2 mới thêm.)

- [ ] **Step 5: Test xanh + mutation check**

Run filter CouponServiceTests → 16/16 pass.

Mutation check (RED compile-time): tạm đổi `ValidateAsync` bỏ cap (`discount = discount` thay vì
`Math.Min(discount, subtotal)`) → test 3 phải fail. Reverse-edit; chạy lại → pass.

- [ ] **Step 6: Build + commit**

Run `dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning.

```powershell
git add ECommerce.Web/Models/Entities/Enums/DiscountType.cs ECommerce.Web/Models/Entities/Coupon.cs ECommerce.Web/Models/ViewModels/CouponFormViewModel.cs ECommerce.Web/Services/CouponValidationResult.cs ECommerce.Web/Services/ICouponService.cs ECommerce.Web/Services/CouponService.cs ECommerce.Web/Data/ApplicationDbContext.cs ECommerce.Web/Data/Migrations ECommerce.Web/Program.cs ECommerce.Web.Tests/Services/CouponServiceTests.cs
git commit -m "feat(coupons): add coupon entity, service and migration"
```

---

### Task 2: Redeem trong transaction tạo đơn

**Files:**
- Modify: `ECommerce.Web/Models/Entities/Order.cs`
- Modify: `ECommerce.Web/Services/IOrderService.cs`, `ECommerce.Web/Services/OrderService.cs`
- Modify: `ECommerce.Web/Controllers/OrdersController.cs`
- Test: `ECommerce.Web.Tests/Services/OrderServiceTests.cs`, `ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs`

**Interfaces:**
- Consumes: `ICouponService.RedeemAsync(string? code, decimal subtotal)`.
- Produces: `CreateOrderFromCartAsync(string userId, ShippingDetails shipping, PaymentMethod method, string? couponCode = null)`.
- `Order` thêm `decimal DiscountAmount`, `string? CouponCode`.

- [ ] **Step 1: Test đỏ cho OrderService**

Thêm vào `OrderServiceTests` (ctor đổi thành `new OrderService(_db, new CouponService(_db), _loggerMock.Object)`):

1. `CreateOrderFromCartAsync_WithValidCoupon_AppliesDiscountAndIncrementsUsage`
   - seed user + product 100k stock 5 + coupon `SAVE10` 10%, limit 5; cart 2 sản phẩm
   - gọi với `"save10"` → `Success`; order `DiscountAmount == 20_000`, `CouponCode == "SAVE10"`,
     `TotalAmount == 180_000`; DB `UsedCount == 1`; giỏ rỗng.
2. `CreateOrderFromCartAsync_WithInvalidCoupon_AbortsWithoutSideEffects`
   - coupon chưa tồn tại; cart 1 sản phẩm; gọi `"NOPE"` → `Success == false`, không có Order,
     `StockQuantity` không đổi, cart item còn.
3. `CreateOrderFromCartAsync_WhenCouponExhausted_AbortsAndKeepsStock`
   - coupon `UsedCount = UsageLimit = 1` → fail, message chứa "đã được sử dụng hết", stock/cart nguyên.

Run filter `OrderServiceTests` → RED (compile-time do signature 4 tham số).

- [ ] **Step 2: Sửa OrderService + interface**

Trong `CreateOrderFromCartAsync`, sau precheck tồn kho và **trước** vòng trừ kho:

```csharp
var subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
var coupon = await _couponService.RedeemAsync(couponCode, subtotal);
if (!coupon.IsValid)
{
    await transaction.RollbackAsync();
    return (false, coupon.Error ?? "Mã giảm giá không hợp lệ.", (int?)null);
}
```
Order khởi tạo thêm `DiscountAmount = coupon.DiscountAmount`, `CouponCode = coupon.Code`,
`TotalAmount = subtotal - coupon.DiscountAmount` (không âm vì service đã cap).

- [ ] **Step 3: Test đỏ cho OrdersController**

Sửa helper `CreateController` (thêm `Mock<ICouponService>? couponService = null`):
- truyền `(couponService ?? new Mock<ICouponService>()).Object` vào ctor **sau** `cartService`;
- setup mặc định: `ValidateAsync(It.IsAny<string?>(), It.IsAny<decimal>())` trả
  `new CouponValidationResult(false, null, null, 0m, null)`.

Thêm 2 test:
1. `Checkout_PassesCouponCodeToOrderService` — model `CouponCode = "SAVE10"`; verify
   `CreateOrderFromCartAsync("user-checkout", It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>(), "SAVE10")`.
2. `Checkout_WhenOrderServiceFails_RedirectsToCheckoutWithCouponCode` — service trả `(false, "Mã giảm giá đã hết hạn.", null)`
   → `RedirectToActionResult` ActionName `"Checkout"`, ControllerName `"Cart"`, route `couponCode == "SAVE10"`.

Cập nhật **mọi** `Setup(s => s.CreateOrderFromCartAsync(...))` hiện có: thêm tham số thứ 4
`It.IsAny<string?>()` (7 chỗ: dòng ~87, 115, 126, 151, 166, 197, 225).

- [ ] **Step 4: Sửa OrdersController**

Thêm field + tham số `ICouponService couponService` (sau `cartService`).
- Trong `if (!ModelState.IsValid)`: sau `model.Cart = ...`, gọi `ValidateAsync` và gán
  `DiscountAmount`, `CouponError`, `CouponDescription`.
- Truyền `model.CouponCode` vào `CreateOrderFromCartAsync`.
- Khi `!success`: `_notyf.Error(message); return RedirectToAction("Checkout", "Cart", new { couponCode = model.CouponCode });`

- [ ] **Step 5: Thêm cột Order + migration**

`Order` thêm 2 property; `ApplicationDbContext` config `DiscountAmount` precision (18,2),
`CouponCode` max 50. Chạy:
`dotnet ef migrations add AddCouponOrderSnapshot --project ECommerce.Web --startup-project ECommerce.Web --output-dir Data/Migrations`
Kiểm tra `Orders.DiscountAmount` NOT NULL **có `defaultValue: 0m`** và `CouponCode` nullable.
Rồi `dotnet ef database update --project ECommerce.Web --startup-project ECommerce.Web`.

- [ ] **Step 6: Test xanh + toàn bộ suite + commit**

Run filter OrderServiceTests và CheckoutControllerTests → pass; chạy toàn bộ → pass; build 0 warning.

```powershell
git add ECommerce.Web/Models/Entities/Order.cs ECommerce.Web/Services/IOrderService.cs ECommerce.Web/Services/OrderService.cs ECommerce.Web/Controllers/OrdersController.cs ECommerce.Web/Data/ApplicationDbContext.cs ECommerce.Web/Data/Migrations ECommerce.Web.Tests/Services/OrderServiceTests.cs ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs
git commit -m "feat(checkout): redeem coupon atomically when creating order"
```

---

### Task 3: Preview coupon ở trang thanh toán

**Files:**
- Modify: `ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs`
- Modify: `ECommerce.Web/Controllers/CartController.cs`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`
- Test: `ECommerce.Web.Tests/Controllers/CartControllerCheckoutTests.cs`

**Interfaces:**
- Consumes: `ICouponService.ValidateAsync(string?, decimal)`.
- Produces: `CheckoutViewModel` thêm `string? CouponCode`, `decimal DiscountAmount`,
  `string? CouponError`, `string? CouponDescription`, computed `decimal PayableTotal`.

- [ ] **Step 1: Test đỏ**

Cập nhật harness `CreateController` thêm `Mock<ICouponService>` (ctor `CartController(cart, notyf, addresses, coupons)`).
Thêm:
1. `Checkout_WithValidCoupon_ShowsDiscountAndPayableTotal` — `ValidateAsync("SAVE10", 10_000_000)` trả
   `(true, null, "SAVE10", 1_000_000m, "Giảm 10%")` → model `DiscountAmount == 1_000_000`,
   `PayableTotal == 9_000_000`, `CouponCode == "SAVE10"`, `CouponError == null`.
2. `Checkout_WithInvalidCoupon_ShowsErrorAndNoDiscount` — trả `(false, "Mã giảm giá đã hết hạn.", null, 0m, null)`
   → `CouponError` đúng, `DiscountAmount == 0`, `PayableTotal == Cart.TotalAmount`.

Run filter → RED.

- [ ] **Step 2: VM + controller**

`CheckoutViewModel`:
```csharp
[StringLength(50)]
public string? CouponCode { get; set; }
public decimal DiscountAmount { get; set; }
public string? CouponError { get; set; }
public string? CouponDescription { get; set; }
public decimal PayableTotal => Math.Max(0m, Cart.TotalAmount - DiscountAmount);
```
`CartController` thêm `ICouponService` (tham số cuối ctor); `Checkout(string? couponCode = null)`:
sau khi xác thực đăng nhập, gọi `ValidateAsync(couponCode, cart.TotalAmount)` và gán
`CouponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim()`,
`DiscountAmount = result.DiscountAmount`, `CouponError = result.Error`,
`CouponDescription = result.Description`.

- [ ] **Step 3: View**

Trên khối `.bg-white ... rounded-3xl` (trước `<form asp-controller="Orders" ...>`), thêm panel GET:
```html
<div class="mb-4 bg-white dark:bg-gray-900 rounded-2xl border ... p-4">
  <form asp-controller="Cart" asp-action="Checkout" method="get" class="flex flex-col sm:flex-row gap-2">
    <input name="couponCode" value="@Model.CouponCode" placeholder="Nhập mã giảm giá" class="flex-1 ..." />
    <button type="submit" class="...">Áp dụng</button>
  </form>
  @if (!string.IsNullOrEmpty(Model.CouponError)) { <p class="mt-2 text-xs text-red-600">@Model.CouponError</p> }
  @if (Model.DiscountAmount > 0) { <p class="mt-2 text-xs text-emerald-600">@Model.CouponDescription — giảm <vc:money amount="@Model.DiscountAmount" /></p> }
</div>
```
Trong form POST thêm `<input type="hidden" asp-for="CouponCode" />`.
Trong phần Totals: chèn dòng "Giảm giá" khi `Model.DiscountAmount > 0`
(hiển thị `-<vc:money amount="@Model.DiscountAmount" />`), và đổi "Tổng thanh toán"
từ `Model.Cart.TotalAmount` sang `Model.PayableTotal`.

- [ ] **Step 4: Test xanh + build + commit**

Filter CartControllerCheckoutTests → pass; full suite; build 0 warning.

```powershell
git add ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs ECommerce.Web/Controllers/CartController.cs ECommerce.Web/Views/Cart/CheckoutPreview.cshtml ECommerce.Web.Tests/Controllers/CartControllerCheckoutTests.cs
git commit -m "feat(checkout): preview coupon discount and applicable total"
```

---

### Task 4: Admin CRUD mã giảm giá

**Files:**
- Modify: `ECommerce.Web/Controllers/AdminController.cs`
- Create: `ECommerce.Web/Views/Admin/Coupons.cshtml`, `ECommerce.Web/Views/Admin/CouponForm.cshtml`
- Modify: `ECommerce.Web/Views/Admin/Index.cshtml` (thêm link)
- Test: none mới (service CRUD đã test ở Task 1; view compile ở build + live verify Task 5)

**Interfaces:**
- Consumes: toàn bộ `ICouponService` (Task 1).

- [ ] **Step 1: Controller**

Thêm `ICouponService` vào ctor (sau `_orderService`). Actions (tất cả `[Authorize(Roles = AppRoles.Admin)]`):

| Action | Method | Hành vi |
| --- | --- | --- |
| `Coupons()` | GET | `View(await _couponService.GetAllAsync())` |
| `CreateCoupon()` | GET | `View("CouponForm", new CouponFormViewModel { StartsOn = today, EndsOn = today.AddDays(30), UsageLimit = 100, IsActive = true })` |
| `CreateCoupon(CouponFormViewModel model)` | POST | invalid → `View("CouponForm", model)`; service success → `_notyf.Success` + redirect `Coupons`; fail → `_notyf.Error` + `View("CouponForm", model)` |
| `EditCoupon(int id)` | GET | không thấy → `NotFound()`; map entity → VM (`Id`, `Code`, `DiscountType`, `Value`, `MinOrderAmount`, `UsageLimit`, `StartsOn`, `EndsOn`, `IsActive`) → `View("CouponForm", vm)` |
| `EditCoupon(CouponFormViewModel model)` | POST | invalid → view; service success/fail toast; fail giữ form, success redirect |
| `ToggleCouponStatus(int id)` | POST | service toggle, toast, redirect `Coupons` |
| `DeleteCoupon(int id)` | POST | service delete, toast, redirect `Coupons` |

POST actions dùng `[ValidateAntiForgeryToken]`; admin actions không cần rate-limit `mutating`
(đã `[Authorize(Roles=Admin)]`, theo pattern hiện có của AdminController).

- [ ] **Step 2: Views**

`Coupons.cshtml` (`@model IReadOnlyList<ECommerce.Web.Models.Entities.Coupon>`): tiêu đề + nút
"Thêm mã" (`asp-action="CreateCoupon"`); bảng cột Mã, Loại, Giá trị, Đơn tối thiểu, Đã dùng/Giới hạn,
Hiệu lực (`StartsOn – EndsOn`), Trạng thái (badge + form POST `ToggleCouponStatus`), thao tác
(Edit link + form POST `DeleteCoupon` có `onsubmit="return confirm('Xóa mã giảm giá này?');"`).
Empty state: "Chưa có mã giảm giá nào."

`CouponForm.cshtml` (`@model CouponFormViewModel`): `<form asp-action="@(Model.Id > 0 ? "EditCoupon" : "CreateCoupon")" method="post">`
+ hidden `Id`; `asp-for` cho Code, DiscountType (`<select asp-for="DiscountType" asp-items="Html.GetEnumSelectList<DiscountType>()">`),
Value, MinOrderAmount, UsageLimit, StartsOn, EndsOn (`type="date"`), IsActive checkbox;
`asp-validation-summary="ModelOnly"` và `asp-validation-for` từng trường; nút Lưu + link Hủy về `Coupons`.

`Views/Admin/Index.cshtml`: thêm cạnh nút "Quản lý kho hàng":
```html
<a asp-action="Coupons" class="..."><i class="fas fa-ticket-alt ..."></i> Mã giảm giá</a>
```

- [ ] **Step 3: Build + commit**

Run `dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning (Razor compile).
Chạy full test suite (không đổi hành vi) → pass.

```powershell
git add ECommerce.Web/Controllers/AdminController.cs ECommerce.Web/Views/Admin/Coupons.cshtml ECommerce.Web/Views/Admin/CouponForm.cshtml ECommerce.Web/Views/Admin/Index.cshtml
git commit -m "feat(admin): add coupon management screens"
```

---

### Task 5: Hiển thị giảm giá trên đơn/email + live verify

**Files:**
- Modify: `ECommerce.Web/Views/Orders/Details.cshtml`
- Modify: `ECommerce.Web/Views/Admin/OrderDetails.cshtml`
- Modify: `ECommerce.Web/Views/Emails/OrderConfirmation.cshtml`

- [ ] **Step 1: Dòng giảm giá**

- `Views/Orders/Details.cshtml`: trong `<tfoot>`, trước dòng "Tổng thanh toán", thêm khi
  `Model.DiscountAmount > 0`: cột label `Giảm giá (@Model.CouponCode):`, cột giá trị
  `-<vc:money amount="@Model.DiscountAmount" />`.
- `Views/Admin/OrderDetails.cshtml`: cùng điều kiện, định dạng `-@Model.DiscountAmount.ToString("N0") đ`.
- `Views/Emails/OrderConfirmation.cshtml`: ngay trên dòng "Tổng cộng", thêm khi
  `order.DiscountAmount > 0`: `Giảm giá (@order.CouponCode): -@order.DiscountAmount.ToString("N0") đ`.

- [ ] **Step 2: Build + full suite**

`dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning; `dotnet test ...` → pass.

- [ ] **Step 3: Live verify (một command, tự teardown)**

Boot dev server port 5080 (`ASPNETCORE_ENVIRONMENT=Development`), script HTTP với cookie jar;
kịch bản:

1. Admin login `admin@ecommerce.local` / `Admin@12345`.
2. `GET /Admin/CouponForm` → token; `POST /Admin/CreateCoupon` mã `B3LIVE<stamp>`,
   Percentage 10, MinOrderAmount 0, UsageLimit 1, StartsOn hôm nay, EndsOn hôm nay+7, IsActive true
   → 302; `GET /Admin/Coupons` chứa mã.
3. Register user mới `b3.<stamp>@technova.local` / `Qa!23456`.
4. Thêm sản phẩm id 3 (còn kho) vào giỏ; `GET /Cart/Checkout?couponCode=<mã>` →
   200, HTML chứa mã, `Giảm giá`, tổng phải trả = subtotal × 0.9 (đối chiếu chuỗi `N0` đã tính trong script).
5. `POST /Orders/Checkout` kèm `CouponCode` → 302 `/Orders/Details/{id}`; trang chi tiết chứa
   `B3LIVE<stamp>` và dòng giảm giá; query DB: `UsedCount == 1`, đơn có `DiscountAmount > 0`,
   `TotalAmount == subtotal - discount`.
6. User thứ hai (register mới) thêm sản phẩm; `GET /Cart/Checkout?couponCode=<mã>` →
   chứa "đã được sử dụng hết"; `POST /Orders/Checkout` cùng mã → 302 về `/Cart/Checkout`,
   không tạo đơn mới, `GET /Cart` vẫn còn sản phẩm.
7. Teardown: kill server + mọi `dotnet` khác `$PID`; assert port 5080 không listen.

Ghi kết quả từng CHECK vào report `.superpowers/sdd/2026-10-19-phase-b3-coupons/report-phase-b3-coupons.md`
kèm commit range, số test trước/sau, side effect DB (user/mã/đơn test).

- [ ] **Step 4: Graph gate**

`node .gitnexus/run.cjs detect-changes --scope compare --base-ref c4afdc6 --repo .`
Yêu cầu không `partial`/`truncated`; risk thường cao vì đụng checkout/order.

---

## Self-review notes

- Spec D-B3-1..9 đều có task: entity/service/migration (T1), snapshot + redeem transaction (T2),
  preview (T3), admin CRUD (T4), hiển thị + verify (T5).
- `ValidateAsync` read-only; chỉ `RedeemAsync` mới tăng `UsedCount`, và chỉ được gọi bên trong
  transaction của `OrderService`.
- Mọi chỗ gọi `CreateOrderFromCartAsync` trong test được liệt kê để thêm tham số thứ 4.
- Không đổi `PaymentService`; nó dùng `order.TotalAmount` nên tự nhận số đã giảm.
