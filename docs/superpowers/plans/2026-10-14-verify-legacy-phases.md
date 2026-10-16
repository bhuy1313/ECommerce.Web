# Verify Legacy Phases (Phase 0 – 8.6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify lại toàn bộ các phase cũ (Phase 0 → 8.6) trong `docs/roadmap.md` vẫn đúng với code hiện tại, phát hiện drift/regression trước khi mở Phase 9.

**Architecture:** Verify-only, không sửa code trong plan này. Mỗi task kiểm tra một phase bằng 4 lớp bằng chứng: (1) GitNexus graph-first (`query`/`context`) để hiểu flow và dependencies, (2) đọc source đối chiếu roadmap, (3) chạy lệnh build/test/migration, (4) smoke thủ công khi cần. Bug và đề xuất cải thiện chỉ ghi nhận vào báo cáo (Luồng B), sửa ở plan riêng. Superpowers điều phối cách làm: `executing-plans`/`subagent-driven-development` để chạy task, `systematic-debugging` khi gặp FAIL, `verification-before-completion` trước mọi verdict.

**Tech Stack:** ASP.NET Core MVC (.NET 10, `ECommerce.Web/`), EF Core + SQL Server LocalDB, ASP.NET Core Identity, xUnit (`ECommerce.Web.Tests/`, 21 test methods / ~23 test cases), Razor + Tailwind CDN (storefront) / Bootstrap 5 (admin cũ) + TechNova Admin.

**Spec:** `docs/roadmap.md` (Phase 0 → 8.6, tất cả `[x]`), đối chiếu với `01-Project-Overview-E-Commerce.docx` cho Non-Goals/Backlog; code thực tế trong `ECommerce.Web/` và `ECommerce.Web.Tests/`.

## Global Constraints

- Verify-only: KHÔNG sửa source, KHÔNG commit code; chỉ được tạo/sửa file báo cáo verify.
- Mọi kết luận PASS/FAIL phải kèm lệnh đã chạy + output thực tế, không kết luận bằng đọc lướt.
- Lệnh build/test chạy từ repo root `C:\Users\hieub\source\repos\ECommerce.Web`.
- Giữ nguyên 21 test methods hiện có; Theory với InlineData tính thành nhiều cases (~23 cases theo roadmap).
- Phase 9 đang Freeze — verify không được đụng tới deploy/production config.
- Backlog (payment gateway, images, coupon, reviews, reporting nâng cao, audit log, background jobs, caching, CI/CD) là Non-Goals — thấy thiếu là đúng, không ghi FAIL.
- Superpowers bắt buộc: chạy task qua `executing-plans` (native) hoặc `subagent-driven-development`; gặp FAIL dùng `systematic-debugging` (giả thuyết → kiểm chứng → kết luận); mọi verdict tuân `verification-before-completion` (lệnh + output thực, không kết luận miệng).
- GitNexus bắt buộc (theo `AGENTS.md`): read-only dùng graph-first — `query({search_query})` cho concept/flow, `context({name})` cho symbol, `route_map` cho route `[Authorize]` — trước khi dùng `grep`/đọc chay; `impact({target, direction: "upstream"})` chỉ chạy khi đề xuất thay đổi để lấy risk/callers/processes; `detect_changes({scope:"all"})` trước mọi commit báo cáo; `risk: UNKNOWN` là chưa kết luận được, phải xác minh thêm bằng text search, không coi là an toàn.
- Hai luồng đầu ra song song: Luồng A (Verdict PASS/FAIL/PARTIAL + bằng chứng) và Luồng B (Đề xuất thay đổi/cải thiện + Severity + risk GitNexus). Luồng B không được sửa code luôn trong plan này.

## Phương Pháp Verify (Superpowers + GitNexus)

Mọi task (T1–T10) tuân cùng một khung 5 bước:

1. **Graph-first (GitNexus `query`/`context`):** trước khi đọc chay, chạy `query({search_query: "<concept của phase>"})` để lấy execution flows, rồi `context({name: "<symbol chính>"})` để lấy callers/callees. Ghi process/flow nào đã tra.
2. **Đối chiếu source:** đọc file trong `Files:` và tick từng dòng roadmap.
3. **Chạy lệnh + smoke:** `dotnet build` / `dotnet test --filter` / `dotnet ef` / smoke browser; paste output thực tế.
4. **FAIL → `systematic-debugging`:** tái hiện → khoanh vùng → ghi file:dòng nguyên nhân, không đoán.
5. **Triage đề xuất (Luồng B):** mỗi phát hiện ngoài verdict phải ghi một dòng đề xuất với Severity:
   - `Blocker`: sai business rule / mất tiền / mất hàng / bypass phân quyền — chặn Go Phase 9.
   - `Major`: regression rõ, cần plan sửa riêng.
   - `Minor`: drift nhỏ (số test, copy, threshold hard-code) — sửa kèm plan sau.
   - `Nice-to-have`: cải thiện (gom constant, thêm log, tách view) — backlog.
   - Đề xuất đụng code phải kèm `impact({target, direction:"upstream"})` + risk (LOW/MEDIUM/HIGH/CRITICAL/UNKNOWN) và cảnh báo HIGH/CRITICAL; `UNKNOWN` phải verify thêm bằng text search.

## Review Focus

- Guest cart merge sau login có mất item không — mong đợi giữ nguyên item guest + cộng dồn vào user cart.
- Concurrent purchase 2 request cùng lúc vào stock cuối cùng — mong đợi đúng 1 thành công, stock không âm.
- Cancel order đã Shipped/Completed có bị chặn + stock không hoàn sai lần 2 — mong đợi chặn + chỉ hoàn 1 lần.
- Admin authorize bypass bằng URL trực tiếp khi chưa login / role Customer — mong đợi redirect Login hoặc 403.
- Low-stock threshold hiển thị trên Dashboard có khớp logic service — mong đợi cùng một ngưỡng, không hard-code 2 nơi khác nhau.

---

### Task 1: Phase 0 Setup + Phase 1 Data Layer

**Files:**
- Read: `ECommerce.Web/appsettings.json`, `ECommerce.Web/appsettings.Development.json`, `ECommerce.Web/ECommerce.Web.csproj`, `ECommerce.Web/Program.cs:16-18,40-49`
- Read: `ECommerce.Web/Data/ApplicationDbContext.cs`, `ECommerce.Web/Data/DbInitializer.cs`, `ECommerce.Web/Models/Entities/Product.cs`, `ECommerce.Web/Models/Entities/Category.cs`, `ECommerce.Web/Models/Entities/Cart.cs`, `ECommerce.Web/Models/Entities/CartItem.cs`, `ECommerce.Web/Models/Entities/Order.cs`, `ECommerce.Web/Models/Entities/OrderItem.cs`, `ECommerce.Web/Models/Entities/Enums/OrderStatus.cs`, `ECommerce.Web/Models/Entities/Enums/PaymentStatus.cs`, `ECommerce.Web/Models/Entities/ApplicationUser.cs`
- Test: `ECommerce.Web.Tests/Helpers/TestDbContextFactory.cs`

**Interfaces:**
- Consumes: Không có (task đầu tiên).
- Produces: Bằng chứng Phase 0/1 PASS/FAIL (connection string, packages, migration list, seed 3 categories + 7 products) cho Task 11 tổng hợp.

- [ ] **Step 1: Đối chiếu config Phase 0 với source**

Mở `appsettings.json` + `.csproj`, xác nhận `DefaultConnection` (LocalDB 2025 Express), `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` tồn tại.

- [ ] **Step 2: Chạy liệt kê migrations**

Run: `dotnet ef migrations list --project ECommerce.Web`
Expected: PASS, liệt kê ít nhất migration `20260925125612_InitialCreate`.

- [ ] **Step 3: Đối chiếu entities + enums + seed**

Mở các file Entities/Enums + `DbInitializer.cs`, xác nhận đủ 6 entities, `OrderStatus` (Pending/Processing/Shipped/Completed/Cancelled), `PaymentStatus`, seed 3 categories + 7 products, `Database.Migrate()` trong `Program.cs:47`.

- [ ] **Step 4: Ghi kết quả Task 1**

Ghi một dòng vào báo cáo: `Phase 0: PASS/FAIL + bằng chứng`, `Phase 1: PASS/FAIL + bằng chứng`. FAIL phải kèm tên file + dòng lệch roadmap.

### Task 2: Phase 2 Authentication & Authorization

**Files:**
- Read: `ECommerce.Web/Controllers/AccountController.cs`, `ECommerce.Web/Controllers/AdminController.cs`, `ECommerce.Web/Models/Constants/AppRoles.cs`, `ECommerce.Web/Models/ViewModels/LoginViewModel.cs`, `ECommerce.Web/Models/ViewModels/RegisterViewModel.cs`, `ECommerce.Web/Program.cs:19-36,66-67`
- Test: smoke thủ công `/Account/Login`, `/Account/Register`, `/Admin`

**Interfaces:**
- Consumes: Vai trò `Admin`, `StoreOperator`, `Customer` từ `AppRoles.cs` (Task 1 xác nhận Identity đã wire).
- Produces: Ma trận Auth PASS/FAIL (Register/Login/Logout/roles/seed admin/authorize) cho Task 11.

- [ ] **Step 1: Đối chiếu Identity wiring**

Xác nhận `AddIdentity<ApplicationUser, IdentityRole>` (password length 8, require digit, unique email), `AddEntityFrameworkStores`, cookie `LoginPath=/Account/Login`, `UseAuthentication/UseAuthorization`, seed role Admin trong `DbInitializer.cs`.

- [ ] **Step 2: Đối chiếu `[Authorize(Roles=...)]` trên route admin**

Run: `rg -n "Authorize.*Roles" ECommerce.Web/Controllers/AdminController.cs`
Expected: PASS khi mọi action admin đều nằm sau authorize; ghi lại roles nào được phép.

- [ ] **Step 3: Smoke Register → Login → Logout → phân quyền**

Run: `dotnet run --project ECommerce.Web` rồi mở browser: Register user mới → Login → Logout → thử vào `/Admin` khi chưa login và khi là Customer.
Expected: PASS khi Register/Login/Logout chạy, `/Admin` redirect Login hoặc 403 cho non-admin.

- [ ] **Step 4: Ghi kết quả Task 2**

Ghi `Phase 2: PASS/FAIL + ma trận 5 mục (Register/Login/Logout/roles/authorize)`.

### Task 3: Phase 3 Catalog (Product + Category)

**Files:**
- Read: `ECommerce.Web/Controllers/ProductsController.cs`, `ECommerce.Web/Controllers/AdminController.cs`, `ECommerce.Web/Views/Products/Index.cshtml`, `ECommerce.Web/Views/Products/Details.cshtml`, `ECommerce.Web/Models/ViewModels/ProductListViewModel.cs`, `ECommerce.Web/Models/ViewModels/ProductFormViewModel.cs`, `ECommerce.Web/Models/ViewModels/CategoryFormViewModel.cs`, `ECommerce.Web/Models/Entities/Product.cs`
- Test: smoke `/Products`, `/Products/Details/{id}`, admin CRUD

**Interfaces:**
- Consumes: Entities `Product`/`Category` từ Task 1.
- Produces: Checklist Catalog PASS/FAIL (list/detail/search/filter/CRUD/archive/validation) cho Task 11.

- [ ] **Step 1: Đối chiếu controller + filter/search**

Mở `ProductsController.cs`, xác nhận list + detail + search + filter theo category đều tồn tại và query đúng `CategoryId`/keyword.

- [ ] **Step 2: Đối chiếu Admin CRUD + archive/deactivate**

Mở `AdminController.cs` (Products/Categories/ProductForm/CategoryForm), xác nhận CRUD Category, CRUD Product, có cờ archive/deactivate (kiểm tra field `IsActive` hoặc tương đương trong `Product.cs`).

- [ ] **Step 3: Đối chiếu Data Annotations validation**

Mở `Product.cs` + `ProductFormViewModel.cs` + `CategoryFormViewModel.cs`, xác nhận `[Required]`/`[Range]`/`[StringLength]` phía server; thử submit form rỗng trên UI.
Expected: PASS khi `ModelState.IsValid == false` và UI hiện lỗi.

- [ ] **Step 4: Ghi kết quả Task 3**

Ghi `Phase 3: PASS/FAIL` cho 5 mục: list/detail, search+filter, CRUD category, CRUD+archive product, validation.

### Task 4: Phase 4 Shopping Cart

**Files:**
- Read: `ECommerce.Web/Controllers/CartController.cs`, `ECommerce.Web/Services/CartService.cs`, `ECommerce.Web/Services/ICartService.cs`, `ECommerce.Web/Views/Cart/Index.cshtml`
- Test: `ECommerce.Web.Tests/Services/CartServiceTests.cs` (5 methods: 1 Theory + 4 Fact)

**Interfaces:**
- Consumes: `Product.Stock` từ Task 1/3.
- Produces: Kết quả Cart (add/update/remove/stock-check/guest-vs-login) + output `dotnet test --filter CartServiceTests` cho Task 8/11.

- [ ] **Step 1: Chạy test CartService**

Run: `dotnet test ECommerce.Web.Tests --filter "FullyQualifiedName~CartServiceTests"`
Expected: PASS toàn bộ 5 methods; ghi số cases thực tế (Theory nở thành nhiều cases).

- [ ] **Step 2: Đối chiếu add/update/remove + kiểm tra stock trước checkout**

Mở `CartService.cs` + `CartController.cs`, xác nhận thêm vào cart, cập nhật số lượng, xóa item, và có check `Stock` trước checkout; thử trên UI thêm quá stock.
Expected: PASS khi bị chặn hoặc clamp đúng stock.

- [ ] **Step 3: Đối chiếu guest vs logged-in cart**

Mở `CartService.cs` tìm phân nhánh session/cookie vs user-id claim; smoke: thêm cart khi chưa login → login → kiểm tra cart còn/gộp.
Expected: PASS khi không mất item (ghi rõ behavior gộp hay giữ riêng đúng code).

- [ ] **Step 4: Ghi kết quả Task 4**

Ghi `Phase 4: PASS/FAIL` cho 4 mục: add, update/remove, stock-check, guest/logged-in.

### Task 5: Phase 5 Checkout & Orders (trọng tâm business rule)

**Files:**
- Read: `ECommerce.Web/Controllers/CartController.cs`, `ECommerce.Web/Controllers/OrdersController.cs`, `ECommerce.Web/Controllers/AdminController.cs`, `ECommerce.Web/Services/OrderService.cs`, `ECommerce.Web/Services/IOrderService.cs`, `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`, `ECommerce.Web/Views/Orders/Index.cshtml`, `ECommerce.Web/Views/Orders/Details.cshtml`
- Test: `ECommerce.Web.Tests/Services/OrderServiceTests.cs` (8 methods: 7 Fact + 1 Theory), `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` (8 Fact)

**Interfaces:**
- Consumes: Cart đã verify (Task 4), `OrderStatus`/`PaymentStatus` (Task 1).
- Produces: Kết quả transaction/cancel/stock-âm/concurrent + output test Order cho Task 8/11. Đây là task rủi ro cao nhất.

- [ ] **Step 1: Chạy test OrderService + Validation**

Run: `dotnet test ECommerce.Web.Tests --filter "FullyQualifiedName~OrderServiceTests|FullyQualifiedName~ValidationAndCalculationsTests"`
Expected: PASS 16 methods; ghi số cases thực tế.

- [ ] **Step 2: Đối chiếu transaction tạo Order (order + items + trừ stock)**

Mở `OrderService.cs`, xác nhận tạo Order nằm trong transaction (explicit `BeginTransaction` hoặc `SaveChanges` atomic + guard), trừ stock cùng lúc, tính total đúng. Ghi số dòng hàm tạo order.

- [ ] **Step 3: Đối chiếu history customer + admin update status + cancel hoàn stock**

Mở `OrdersController.cs` (history customer) + `AdminController.cs` (xem tất cả + update status: Orders/OrderDetails), xác nhận cancel theo business rule (chỉ Pending/Processing được hủy, hoàn stock đúng số lượng, chặn hủy lần 2).

- [ ] **Step 4: Đối chiếu chặn stock âm + concurrent safeguard**

Tìm guard `Stock < quantity` và cơ chế concurrent (row-version/concurrency-token hoặc re-check trong transaction) trong `OrderService.cs`; thử đặt 2 request cùng lúc vào stock = 1 (nếu môi trường cho phép).
Expected: PASS khi stock không âm và đúng 1 request thắng; nếu không tái hiện được thì ghi `NOT-REPRODUCED + dòng guard`.

- [ ] **Step 5: Ghi kết quả Task 5**

Ghi `Phase 5: PASS/FAIL` cho 5 mục: transaction, history, admin-update, cancel-hoàn-stock, anti-negative/concurrent.

### Task 6: Phase 6 Admin Dashboard

**Files:**
- Read: `ECommerce.Web/Controllers/AdminController.cs`, `ECommerce.Web/Models/ViewModels/AdminDashboardViewModel.cs`, `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs`, `ECommerce.Web/Views/Admin/Index.cshtml`, `ECommerce.Web/Views/Admin/Users.cshtml`, `ECommerce.Web/Views/Admin/EditUserRoles.cshtml`
- Test: smoke `/Admin`, `/Admin/Users`

**Interfaces:**
- Consumes: Orders (Task 5), roles (Task 2).
- Produces: Checklist Dashboard (revenue/orders/products/recent/low-stock/users-roles) cho Task 11.

- [ ] **Step 1: Đối chiếu KPI + recent orders + low-stock**

Mở `AdminController.cs` + `Index.cshtml` + `AdminDashboardViewModel.cs`, xác nhận doanh thu, số đơn, số sản phẩm, bảng đơn gần đây, cảnh báo low-stock; ghi ngưỡng low-stock (so với Review Focus: có hard-code 2 nơi không).

- [ ] **Step 2: Đối chiếu quản lý user + phân vai an toàn**

Mở `Users.cshtml` + `EditUserRoles.cshtml` + action tương ứng, xác nhận list user + số đơn, form gán role có chống tự hạ quyền admin cuối cùng / chống leo thang (ghi cơ chế tìm thấy, nếu không có thì ghi MISSING).

- [ ] **Step 3: Smoke Dashboard với seed data**

Run app + login Admin, mở `/Admin`, chụp/screenshot số liệu, so với DB seed.
Expected: PASS khi số liệu khớp query thực, không phải số cứng.

- [ ] **Step 4: Ghi kết quả Task 6**

Ghi `Phase 6: PASS/FAIL` cho 4 mục: KPI, recent orders, low-stock, users-roles.

### Task 7: Phase 7 Error Handling & Logging

**Files:**
- Read: `ECommerce.Web/Middlewares/GlobalExceptionMiddleware.cs`, `ECommerce.Web/Program.cs:40-62`, `ECommerce.Web/Controllers/HomeController.cs`, `ECommerce.Web/Views/Shared/Error.cshtml`
- Test: smoke `/Home/Error`, URL 404, throw thử

**Interfaces:**
- Consumes: Pipeline trong `Program.cs` (Task 1).
- Produces: Ma trận error-handling (UseExceptionHandler/Error page/middleware/ILogger) cho Task 11.

- [ ] **Step 1: Đối chiếu pipeline lỗi**

Xác nhận `UseGlobalExceptionHandling()`, `UseExceptionHandler("/Home/Error")` (non-dev), `UseStatusCodePagesWithReExecute("/Home/Error/{0}")`, `UseHsts`, trang `Error.cshtml` thân thiện.

- [ ] **Step 2: Đối chiếu middleware tập trung + ILogger**

Mở `GlobalExceptionMiddleware.cs`, xác nhận catch tập trung + log qua `ILogger` với level hợp lý; chạy `rg -n "ILogger" ECommerce.Web --glob "*.cs"` và ghi các nơi log.

- [ ] **Step 3: Smoke 404 + 500**

Mở URL không tồn tại và action ném exception (nếu có route test), xác nhận về trang lỗi thân thiện, không lộ stack trace ở non-dev.

- [ ] **Step 4: Ghi kết quả Task 7**

Ghi `Phase 7: PASS/FAIL` cho 3 mục: error page, middleware, logging.

### Task 8: Phase 8 Testing (toàn bộ suite)

**Files:**
- Read: `ECommerce.Web.Tests/ECommerce.Web.Tests.csproj`, `ECommerce.Web.Tests/Helpers/TestDbContextFactory.cs`, `ECommerce.Web.Tests/Services/CartServiceTests.cs`, `ECommerce.Web.Tests/Services/OrderServiceTests.cs`, `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs`
- Test: toàn bộ suite

**Interfaces:**
- Consumes: Kết quả test lẻ từ Task 4 + Task 5 để đối chiếu.
- Produces: Verdict `dotnet build` + `dotnet test` toàn suite + danh sách test đỏ (nếu có) cho Task 11.

- [ ] **Step 1: Chạy build sạch**

Run: `dotnet build ECommerce.Web.slnx`
Expected: PASS, 0 error. Ghi warnings nếu có.

- [ ] **Step 2: Chạy toàn bộ test suite**

Run: `dotnet test ECommerce.Web.slnx`
Expected: PASS; đối chiếu với claim roadmap "23/23" — ghi số thực tế (21 methods, N cases sau khi Theory nở). Lệch số thì ghi DRIFT chứ không FAIL logic.

- [ ] **Step 3: Đối chiếu coverage business rules**

Xác nhận 4 nhóm được cover: stock update, cancel order, order total, rule validation — map mỗi nhóm tới tên test method cụ thể.

- [ ] **Step 4: Ghi kết quả Task 8**

Ghi `Phase 8: PASS/FAIL + số tests thực tế + danh sách test đỏ (nếu có)`.

### Task 9: Phase 8.5 UI Storefront (TechNova template e_commerce_app.html)

**Files:**
- Read: `e_commerce_app.html`, `ECommerce.Web/Views/Shared/_Layout.cshtml`, `ECommerce.Web/Views/Shared/_LoginPartial.cshtml`, `ECommerce.Web/Views/Home/Index.cshtml`, `ECommerce.Web/Views/Products/Index.cshtml`, `ECommerce.Web/Views/Products/Details.cshtml`, `ECommerce.Web/Views/Cart/Index.cshtml`, `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`, `ECommerce.Web/Views/Orders/Index.cshtml`, `ECommerce.Web/Views/Orders/Details.cshtml`, `ECommerce.Web/Views/Account/Login.cshtml`, `ECommerce.Web/Views/Account/Register.cshtml`, `ECommerce.Web/Views/Admin/_ViewStart.cshtml`, `ECommerce.Web/Views/Shared/_AdminLayout.cshtml`
- Test: smoke visual 8 trang storefront

**Interfaces:**
- Consumes: Catalog/Cart/Orders/Auth đã verify (Task 2–5).
- Produces: Checklist ghép UI storefront (layout/CSS isolation/cart countộng/validation) cho Task 11.

- [ ] **Step 1: Đối chiếu layout + cách ly CSS**

Xác nhận `_Layout.cshtml` theo TechNova (Tailwind CDN, FontAwesome 6, Inter), `_AdminLayout.cshtml` + `Views/Admin/_ViewStart.cshtml` giữ Bootstrap 5, admin và storefront không lẫn CSS.

- [ ] **Step 2: Đối chiếu _LoginPartial + cart count động**

Mở `_LoginPartial.cshtml`, xác nhận dùng `ICartService` hiển thị số lượng thực; smoke: thêm item → số trên header tăng không cần reload cứng.

- [ ] **Step 3: Smoke 8 trang storefront**

Mở lần lượt: `/` (hero/danh mục hot/bán chạy), `/Products` (filter+search+lưới), `/Products/Details` (tồn kho/tăng-giảm/add), `/Cart`, `/Cart/CheckoutPreview` (form xác nhận), `/Orders` + `/Orders/Details`, `/Account/Login`, `/Account/Register`.
Expected: PASS khi đúng layout TechNova + validation hiện lỗi; ghi trang nào vỡ layout.

- [ ] **Step 4: Ghi kết quả Task 9**

Ghi `Phase 8.5: PASS/FAIL` theo 4 nhóm trong roadmap + danh sách trang lỗi.

### Task 10: Phase 8.6 UI Admin Portal Redesign

**Files:**
- Read: `ECommerce.Web/Views/Shared/_AdminLayout.cshtml`, `ECommerce.Web/Views/Admin/Index.cshtml`, `ECommerce.Web/Views/Admin/Products.cshtml`, `ECommerce.Web/Views/Admin/ProductForm.cshtml`, `ECommerce.Web/Views/Admin/Categories.cshtml`, `ECommerce.Web/Views/Admin/CategoryForm.cshtml`, `ECommerce.Web/Views/Admin/Orders.cshtml`, `ECommerce.Web/Views/Admin/OrderDetails.cshtml`, `ECommerce.Web/Views/Admin/Users.cshtml`, `ECommerce.Web/Views/Admin/EditUserRoles.cshtml`
- Test: smoke visual 7 nhóm trang admin

**Interfaces:**
- Consumes: Admin backend (Task 6) + layout cách ly (Task 9).
- Produces: Checklist 7 nhóm trang admin TechNova cho Task 11.

- [ ] **Step 1: Đối chiếu khung AdminLayout**

Xác nhận sidebar cố định/thu gọn, topbar, Tailwind, FontAwesome 6, Inter, flash alerts (TempData) trong `_AdminLayout.cshtml`.

- [ ] **Step 2: Đối chiếu 6 nhóm trang**

Mở từng view: Dashboard (KPI + phân tích trạng thái + low-stock + recent), Products/ProductForm (lọc category + active), Categories/CategoryForm (số lượng sp), Orders/OrderDetails (tabs trạng thái + update), Users/EditUserRoles (số đơn + phân quyền an toàn). Ghi mục nào thiếu so roadmap.

- [ ] **Step 3: Smoke CRUD admin end-to-end**

Login Admin: tạo category → tạo product → đổi trạng thái order → gán role; xác nhận mỗi thao tác persist sau reload.
Expected: PASS cả 4 thao tác; FAIL thao tác nào thì ghi kèm URL + lỗi.

- [ ] **Step 4: Ghi kết quả Task 10**

Ghi `Phase 8.6: PASS/FAIL` theo 7 dòng roadmap.

### Task 11: Tổng hợp báo cáo + cập nhật roadmap

**Files:**
- Create: `docs/verify-legacy-phases-report.md`
- Modify: `docs/roadmap.md` (chỉ khi phát hiện checkbox sai sự thật)
- Test: `dotnet build ECommerce.Web.slnx` + `dotnet test ECommerce.Web.slnx` (lần cuối chốt số liệu)

**Interfaces:**
- Consumes: Kết quả Task 1–10.
- Produces: Báo cáo verify cuối cùng + roadmap hiệu chỉnh (nếu cần). Không sửa source.

- [ ] **Step 1: Chạy build + test chốt số liệu**

Run: `dotnet build ECommerce.Web.slnx; if ($?) { dotnet test ECommerce.Web.slnx }`
Expected: Ghi output thực tế (số passed/failed/skipped) vào báo cáo.

- [ ] **Step 2: Viết báo cáo `docs/verify-legacy-phases-report.md` (2 luồng)**

Luồng A — Verdict: bảng 11 phases × (PASS/FAIL/PARTIAL + bằng chứng + lệnh + GitNexus flow/symbol đã tra), danh sách DRIFT (vd. số test 21 methods vs claim 23).
Luồng B — Đề xuất thay đổi/cải thiện: bảng `| # | Nguồn (Task/Phase) | Mô tả | Severity (Blocker/Major/Minor/Nice-to-have) | Đề xuất sửa | GitNexus impact + risk | Plan sửa riêng? |`, trong đó mọi dòng đụng code phải có `impact({target, direction:"upstream"})` + risk; HIGH/CRITICAL gắn cờ cảnh báo, UNKNOWN ghi rõ "chưa kết luận — cần text search thêm".

- [ ] **Step 3: Graph change check + hiệu chỉnh `docs/roadmap.md` nếu checkbox sai**

Run: `detect_changes({scope: "all"})` (MCP) hoặc `node .gitnexus/run.cjs detect-changes --scope all --repo .`; `partial:true`/`truncated:true` là chưa sạch — phải re-run. Chỉ bỏ `[x]` hoặc thêm ghi chú khi có bằng chứng FAIL; mỗi sửa phải dẫn link tới mục báo cáo.

- [ ] **Step 4: Kết luận Go/No-Go Phase 9**

Ghi một dòng: `Go Phase 9` chỉ khi Task 5 + Task 8 PASS tuyệt đối và không còn Blocker; ngược lại ghi `No-Go + danh sách điều kiện tiên quyết (Blocker/Major còn mở)`.
