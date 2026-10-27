# Báo cáo Verify Phase cũ (Phase 0 → 8.6)

Ngày: 2026-10-14 · Plan: `docs/superpowers/plans/2026-10-14-verify-legacy-phases.md`
Phương pháp: Superpowers (`executing-plans` inline, `verification-before-completion`, `systematic-debugging` khi FAIL) + GitNexus graph-first (`query`/`context` trước `grep`, `impact` cho đề xuất, `route_map`).
Chốt số liệu: `dotnet build ECommerce.Web.slnx` → 0 warning, 0 error · `dotnet test ECommerce.Web.slnx` → **23/23 PASS** (khớp roadmap, NO DRIFT).

## Luồng A — Verdict

| Phase | Verdict | Bằng chứng (lệnh + output thực) |
|---|---|---|
| 0 Setup | PASS | `DefaultConnection` LocalDB/ECommerceDb (`appsettings.json:3`); 3 packages Identity/SqlServer/Tools (`ECommerce.Web.csproj:10-15`); `Database.Migrate()` (`Program.cs:47`) |
| 1 Data Layer | PASS | 6 DbSets (`ApplicationDbContext.cs:14-19`); `OrderStatus` 5 giá trị; `PaymentStatus` 3 giá trị; `dotnet ef migrations list` → `20260925125612_InitialCreate` (applied); seed 3 categories + 7 products (`DbInitializer.cs:47-60`); graph `context(ApplicationDbContext)` epistemic exact |
| 2 Auth | PASS 5/5 | Register (auto-Customer + signin + merge cart), Login (merge cart), Logout `[Authorize]` (`AccountController.cs`); 3 roles + seed admin; `AdminController` class-level `[Authorize(Roles=Admin)]`; live: `/Account/Login` 200, `/Admin` anon 302 redirect; `route_map` = 0 API routes (MVC app, đúng) |
| 3 Catalog | PASS 5/5 | List + search + filter category + active-only, Details 404 (`ProductsController.cs`); Admin Create/Edit/Toggle Category + Product (archive qua `IsActive`, không hard-delete theo thiết kế); Data Annotations cả 2 FormViewModel + check `ModelState` |
| 4 Cart | PASS 4/4 | `dotnet test --filter CartServiceTests` → 6/6 cases; add/update/remove; clamp theo stock (`CartService.cs:115,143-145,183-185,229-231`); guest cookie-cart + `MergeGuestCartAsync` cộng dồn capped-stock (`:338-375`), gọi ở Register + Login |
| 5 Checkout & Orders | PASS 4/5 + PARTIAL 1 | `dotnet test --filter OrderServiceTests\|ValidationAndCalculationsTests` → 17/17; transaction + precheck + trừ stock + clear cart (`OrderService.cs:31-101`); history customer + admin update (`:104-128,188-260`); cancel chặn Cancelled/Shipped/Completed + hoàn stock + Paid→Refunded (`:130-186`); `OrdersController` user-scoped + antiforgery. **PARTIAL: concurrent safeguard** — không có concurrency token, 2 txn đồng thời có thể cùng qua precheck (READ COMMITTED) → stock âm; NOT-REPRODUCED live |
| 6 Admin Dashboard | PASS 4/4 | KPI (revenue trừ Cancelled, đếm theo status, products/categories/users), recent Take(6), low-stock Take(10) (`AdminController.cs:42-84`); Users + order count; `EditUserRoles` có guard tự-hạ-quyền + last-admin (`:508-523`) |
| 7 Error & Logging | PASS 3/3 | `UseGlobalExceptionHandling` + `UseExceptionHandler(/Home/Error)` + `ReExecute` + HSTS (`Program.cs:52-61`); middleware log Error (method/path/user/requestId) + JSON ProblemDetails cho API/rethrow cho HTML; `ILogger` ở 6 files; live URL lạ → 404 sạch; nhánh 500 chỉ code-verified |
| 8 Testing | PASS | Build 0/0; test 23/23 đúng claim roadmap; coverage map đủ 4 nhóm (stock update, cancel, order total, rule validation) |
| 8.5 Storefront | PASS | Tailwind CDN + FA6 + Inter cả 2 layout; cách ly CSS (`Admin/_ViewStart` → `_AdminLayout`); cart count động `ICartService` ở `_Layout:1-3` (roadmap ghi `_LoginPartial` — behavior đúng, sai tên file); live: `/`, `/Products`, `/Details/1`, `/Cart` 200; `/Cart/Checkout` 302 đúng code; `/Orders*` anon 302 đúng; `/Login`, `/Register` 200 |
| 8.6 Admin Portal | PASS | Sidebar/backdrop/flash alerts; tabs + badges trạng thái; filter + active; live login admin 302→`/`; `/Admin` 200 dashboard; `/Products` 200 có `Wireless Mouse` (seed live); `/Orders`, `/Users` 200; CRUD end-to-end: POST CreateCategory 302 + reload PERSIST=True |

DRIFT: không có (23/23 khớp claim; migration đã applied; seed live).
Review Focus (5/5 đã trả lời): guest-merge cộng dồn capped-stock · concurrent PARTIAL (xem Major-1) · cancel chặn Shipped/Completed + chống hủy 2 lần · `/Admin` anon 302 → Login · low-stock `<10` khớp giá trị nhưng hard-code 2 nơi (xem Minor-1).

## Luồng B — Đề xuất thay đổi / cải thiện

| # | Nguồn | Mô tả | Severity | Đề xuất | GitNexus impact + risk |
|---|---|---|---|---|---|
| 1 | T5/Phase 5 | Thiếu optimistic concurrency khi trừ stock: 2 checkout đồng thời vào stock cuối có thể cùng qua precheck → stock âm | Major | ~~Thêm concurrency token hoặc UPDATE atomic~~ **RESOLVED 2026-10-14:** trừ kho bằng `UPDATE … WHERE Stock>=qty` atomic (`OrderService.cs`), detach tracker chống ghi đè mù; +1 boundary test; xUnit 24/24, Playwright orders+cart 6/6 | `impact` file-scoped = LOW (1 caller) |
| 2 | T6/Phase 6 | Ngưỡng low-stock `<10` hard-code 2 nơi (`AdminController.cs:47`, `Views/Admin/Products.cshtml:115`) | Minor | ~~Gom về 1 shared constant~~ **RESOLVED 2026-10-14:** `AppConstants.LowStockThreshold`, dùng ở cả 3 chỗ (controller + 2 views); build 0/0, xUnit 24/24, live smoke OK | impact(Index, upstream)=UNKNOWN 0 callers (MVC routing — expected), đổi tương đương giá trị |
| 3 | T2/Phase 2 | Role `StoreOperator` được seed/gán ở UI nhưng không có `[Authorize]` riêng nào dùng → thực tế ngang `Customer` | Minor | ~~Định nghĩa quyền / ghi reserved~~ **RESOLVED 2026-10-14 theo quyết định user: giữ RESERVED** — gắn XML comment trong `AppRoles.cs`, không đổi behavior | Comment-only, không cần impact |
| 4 | Playwright suite | 7 spec files assert copy/locator cũ, 0/21 pass — suite stale sau redesign TechNova (Phase 8.5/8.6) | Major | ~~Cập nhật locators + copy theo UI thực~~ **RESOLVED 2026-10-14: 21/21 PASS** (spec-only, không đụng source) | — |

Ghi chú: smoke T10 để lại 1 row `VerifyCat_HHMMSS` (active) trong dev LocalDB — dọn khi rảnh (ToggleCategoryStatus để vô hiệu hóa).
Playwright suite (7 files, 21 tests) — sau khi căn lại spec theo UI thực: **21/21 PASS** (2026-10-14). Lần chạy đầu 0/21 do spec drift (assert copy cũ: title `ECommerce.Web`, heading `Welcome`, `Đăng ký tài khoản`, link `Quản trị (Admin)`, `Mã phản hồi: 500`…) — đã loại trừ encoding bug bằng byte-level (`0xE1`/`0xFD`). Các lỗi phụ phát hiện khi fix: submit `button[type=submit]` không scope dính form search header; input `max=15` chặn submit 99 (gỡ max để test safeguard server); cell trùng `$78.00` cần `.first()`; lịch sử đơn dùng card không phải table. Xem đề xuất #4 (RESOLVED).
Chưa chạy: nhánh 500 tái hiện live; concurrent test 2 txn.

## Roadmap

Không cần sửa checkbox nào — mọi `[x]` Phase 0→8.6 đứng vững. Đề xuất ghi chú 1 dòng ở Phase 5: “concurrent safeguard ở mức transaction+precheck (xem báo cáo Major-1)” — tùy chủ repo quyết.

## Go / No-Go Phase 9

**Go:** xUnit 24/24 + Playwright 21/21 đều xanh; 2 Major + 2 Minor đã resolved hết. Việc tồn đọng duy nhất: dọn rows test (`VerifyCat_*`, `Audio *`, `Hi-Fi *`, users/order test) trong dev DB. Đủ điều kiện mở băng Phase 9.
