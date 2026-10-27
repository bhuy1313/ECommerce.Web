# Phase B5 — Shipping & tax: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans.

**Goal:** Tính phí vận chuyển theo phương thức admin cấu hình + thuế VAT, hiển thị và lưu snapshot vào đơn.

**Architecture:** `ShippingMethod` + `ShippingCalculator` (đọc DB/cấu hình) → checkout preview chọn phương thức → `OrderService` tự quote trong transaction → snapshot `ShippingFee`/`TaxAmount` → admin CRUD.

**Tech Stack:** ASP.NET Core MVC, EF Core 10, Options pattern, SQLite in-memory test, xUnit + Moq.

**Spec:** `docs/superpowers/specs/2026-10-22-phase-b5-shipping-tax-design.md`

## Global Constraints

- .NET 10, `0 Warning(s), 0 Error(s)`.
- POST có `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("mutating")]`; admin action `[Authorize(Roles = AppRoles.Admin)]`.
- Không tin phí/thuế từ client; chỉ nhận `ShippingMethodId`.
- Tiếng Việt đúng D-B5-5/6/10.
- Test plain `Assert`, SQLite in-memory, giữ connection.
- Không đổi hành vi coupon/stock/payment hiện có.

## Review Focus

- Request POST sửa `ShippingMethodId` sang id không tồn tại/bị tắt → không tạo đơn, không trừ kho.
- Không có phương thức hoạt động → checkout không cho đặt, không chia 0.
- Coupon + ship + thuế: thuế tính trên subtotal sau giảm, tổng không âm.
- Admin xóa/đổi phí phương thức sau khi đặt → đơn cũ giữ snapshot, không đổi.
- Làm tròn thuế nửa lên (AwayFromZero) và rate cấu hình ngoài [0,1] bị kẹp.

---

### Task 1: ShippingMethod, TaxOptions, calculator, migration

**Files:** `Models/Entities/ShippingMethod.cs`, `Models/Configuration/TaxOptions.cs`, `Services/ShippingQuote.cs`, `Services/IShippingCalculator.cs`, `Services/ShippingCalculator.cs`, `Data/ApplicationDbContext.cs`, `Program.cs`, `appsettings.json`, migration `*_AddShippingAndTax`, test `Services/ShippingCalculatorTests.cs`.

**Interfaces:** đúng D-B5-11; `ShippingMethod` theo D-B5-1.

- [x] **Step 1: Test đỏ** `GetActiveMethods_ReturnsOnlyActiveOrdered`, `Quote_ComputesFeeTaxAndTotal`, `Quote_RoundsTaxAwayFromZero`, `Quote_WhenMethodInactive_ReturnsNull`, `Quote_WhenMethodMissing_ReturnsNull`, `Quote_WhenIdNull_ReturnsNull`, `Quote_ClampsNegativeDiscountedSubtotalToZero`, `TaxRate_OutsideRange_IsClamped` (Options rate 1.5 → 1; -0.2 → 0).
- [x] **Step 2: Chạy fail.**
- [x] **Step 3: Implement** entity + options + calculator + DbContext precision/config + DI (`IShippingCalculator` scoped, `Configure<TaxOptions>`) + `"Tax": { "Rate": 0.1 }`.
- [x] **Step 4: Migration + seed 2 methods** (D-B5-2) + `database update`.
- [x] **Step 5: Full suite + mutation check** (bỏ clamp thuế phải fail test) + commit `feat(shipping): add shipping methods, tax calculator and migration`.

---

### Task 2: Checkout preview chọn phương thức + phí/thuế

**Files:** `Models/ViewModels/CheckoutViewModel.cs`, `Models/ViewModels/ShippingMethodOption.cs`, `Controllers/CartController.cs`, `Views/Cart/CheckoutPreview.cshtml`, test `Controllers/CartControllerCheckoutTests.cs`.

**Interfaces:** `CheckoutViewModel` thêm `ShippingMethodId`, `ShippingMethods`, `ShippingFee`, `TaxRate`, `TaxAmount`, `ShippingError`; `PayableTotal = max(0, TotalAmount - DiscountAmount + ShippingFee + TaxAmount)`.

- [x] **Step 1: Test đỏ** `Checkout_WhenActiveMethods_SelectsFirstAndComputesFees`, `Checkout_WhenNoMethods_ShowsShippingErrorAndZeroFees`; cập nhật helper ctor.
- [x] **Step 2: Chạy fail.**
- [x] **Step 3: Implement** controller lấy methods + quote trên `cart.TotalAmount - discount`; view radio + dòng phí/thuế + tổng; disable nút khi không có phương thức.
- [x] **Step 4: Full suite + mutation check** (bỏ cộng phí vào `PayableTotal` phải fail test) + commit `feat(checkout): select shipping method and preview fees`.

---

### Task 3: OrderService quote trong transaction + snapshot + hiển thị

**Files:** `Models/Entities/Order.cs`, `Services/IOrderService.cs`, `Services/OrderService.cs`, `Controllers/OrdersController.cs`, migration `*_AddOrderShippingTax`, `Views/Orders/Details.cshtml`, `Views/Admin/OrderDetails.cshtml`, `Views/Emails/OrderConfirmation.cshtml`, `Views/Products/Details.cshtml` (ghi chú giá), tests `Services/OrderServiceTests.cs`, `Controllers/CheckoutControllerTests.cs`.

**Interfaces:** `CreateOrderFromCartAsync(userId, shipping, method, couponCode = null, shippingMethodId = null)`; `Order.ShippingFee`, `Order.TaxAmount`.

- [x] **Step 1: Test đỏ service**: `CreateOrderFromCartAsync_WhenQuoteValid_StoresFeesAndTotal`, `..._WhenShippingMethodInvalid_RollsBackAndKeepsStock`, `..._WithCouponAndShipping_TaxesDiscountedSubtotal`; cập nhật mọi ctor test cũ với calculator mock.
- [x] **Step 2: Test đỏ controller**: `Checkout_WhenShippingMethodMissing_ReRendersWithShippingError`, `Checkout_PassesShippingMethodIdToOrderService`.
- [x] **Step 3: Implement** service quote trong transaction (sau coupon), snapshot; controller validate method + truyền id; repopulate preview khi lỗi.
- [x] **Step 4: View** hiển thị phí/thuế ở 3 nơi + ghi chú giá; migration cột default 0.
- [x] **Step 5: Full suite + mutation check** (bỏ cộng thuế vào TotalAmount phải fail) + commit `feat(orders): snapshot shipping fee and tax on order`.

---

### Task 4: Admin shipping methods + live verify

**Files:** `Models/ViewModels/ShippingMethodFormViewModel.cs`, `Controllers/AdminController.cs`, `Views/Admin/ShippingMethods.cshtml`, `Views/Admin/ShippingMethodForm.cshtml`, `Views/Admin/Index.cshtml`, `ECommerce.Web.Tests/Controllers/AdminAuthorizationTests.cs`.

- [x] **Step 1: Cập nhật ma trận quyền** (5 action, fail trước).
- [x] **Step 2: Implement CRUD** như D-B5-9; list hiển thị thuế suất; form validate.
- [x] **Step 3: Build + full suite.**
- [x] **Step 4: Live verify** (port 5080, tự teardown): admin xem list/tạo method `B5LIVE` phí 25.000; user mua sản phẩm; checkout GET có method + phí + thuế 10% + tổng khớp; POST đơn → DB `ShippingFee`, `TaxAmount`, `TotalAmount` khớp; details/email hiển thị; POST forged `ShippingMethodId=999999` → không tạo đơn, kho không đổi; method bị tắt → checkout báo lỗi.
- [x] **Step 5: Graph gate + report** `detect-changes --scope compare --base-ref 1f35832`; report `.superpowers/sdd/2026-10-22-phase-b5-shipping-tax/report-phase-b5-shipping-tax.md`; commit `feat(admin): add shipping method management`.
