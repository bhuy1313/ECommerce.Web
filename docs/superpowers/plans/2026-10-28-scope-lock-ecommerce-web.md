# Kế hoạch khóa phạm vi (Scope Lock) — ECommerce.Web

| Trường | Giá trị |
|---|---|
| **Ngày lập** | 2026-10-28 |
| **Trạng thái** | 📋 Đã chốt, **CHƯA thực thi** |
| **Mục đích** | Chốt bản demo cho CV/portfolio; đóng băng phần vượt mức; bịt các điểm chết trong luồng mua hàng |
| **Phạm vi thay đổi khi thực thi** | Chỉ UI guard + admin nav + README + docs. **Không** đổi logic nghiệp vụ, không thêm entity/bảng, không thêm package |
| **Baseline commit** | `main` @ `b78044d` (sau khi xóa 2 template HTML + thêm `.gitattributes`) |
| **Ước thời gian thực thi** | 1.5 – 2.5 giờ (kế hoạch đã chi tiết hóa giải pháp & số dòng) |

> **Ghi chú Plan mode:** file này chỉ là kế hoạch. Muốn thực thi phải chuyển khỏi Plan mode.
> Mọi số dòng trong tài liệu này đúng tại thời điểm 2026-10-28 — **kiểm lại bằng `Select-String` trước khi sửa**.

---

## 0. Quyết định đã chốt

| # | Vấn đề | Quyết định | Hệ quả |
|---|---|---|---|
| **1** | VNPay hiển thị khi chưa cấu hình | **1A — Ẩn radio khi thiếu config** | Không cần nút "Thanh toán lại"; không cần credentials sandbox; code VNPay giữ nguyên |
| **2** | Admin sidebar thiếu Coupons/Reviews/Shipping | **Có — thêm 3 link** | Tính năng P1 trở nên khám phá được |
| **3** | README có thông tin sai | **Có — sửa ngay trong đợt này** | README khớp 100% thực tế |
| **4** | Lưu plan | **Có — file này** | `C:\Users\hieub\.opencode\plan\2026-10-28-scope-lock-ecommerce-web.md` |

**Hệ quả của 1A (quan trọng):** vì VNPay bị ẩn, rủi ro "đơn kẹt chờ thanh toán không có đường thoát" **không còn xuất hiện trong bản demo**. Do đó task "thêm nút Thanh toán lại" bị **hủy** — không làm, tránh mở rộng phạm vi.

---

## 1. Định nghĩa "đủ" cho bản demo

"Đủ" đo bằng 5 điều kiện kiểm chứng được, không đo bằng số tính năng.

| # | Tiêu chí | Cách kiểm | Hiện tại |
|---|---|---|---|
| **D1** | Golden path chạy trọn vẹn, không điểm chết | 5 bước §5, chạy tay | ✅ Đạt |
| **D2** | Không nút nào bấm vào là hỏng | Mọi control hiển thị đều hoạt động hoặc bị ẩn | ❌ **Chưa** (VNPay) |
| **D3** | Người lạ hiểu trong 60 giây | README + trang chủ nói đúng sự thật | ❌ **Chưa** (README sai) |
| **D4** | Admin chứng minh được vòng vận hành | Xem đơn → đổi trạng thái; sửa sản phẩm; duyệt review | ✅ Đạt |
| **D5** | Có bằng chứng kỹ thuật | `dotnet test` 266/266; `/healthz` 200 | ✅ Đạt |

**Kết luận:** chỉ cần đóng D2 và D3 là bản demo đạt "đủ". Không cần thêm bất kỳ tính năng nào.

---

## 2. Cơ chế khóa — 3 lớp

| Lớp | Cơ chế | Chặn được gì |
|---|---|---|
| **L1 — Cổng hiển thị** | Tính năng chưa cấu hình → ẩn bằng điều kiện config trong code, không dựa vào trí nhớ | Người xem bấm vào tính năng nửa vời |
| **L2 — Cổng quy trình** | Mọi thay đổi phải qua: `dotnet test` (266) + 5 bước golden path + `detect-changes` | Sửa lặt vặt làm vỡ luồng chính |
| **L3 — Cổng tài liệu** | README/roadmap chỉ mô tả thứ chạy được thật | Kỳ vọng sai → thất vọng lúc demo |

**Nguyên tắc gốc: KHÔNG XÓA, CHỈ ĐÓNG BĂNG.**
Không xóa tính năng nào. Những thứ "dư" chính là điểm khác biệt so với project demo trung bình; chỉ cần vô hiệu hóa đúng cách.

---

## 3. Kiểm kê tính năng — 4 nhóm

### 3.1 CORE — phải hoàn hảo, cấm đụng

| Tính năng | Vì sao core |
|---|---|
| Đăng ký / đăng nhập / lockout 5 lần–5 phút | Cửa vào mọi thứ |
| Catalog + tìm kiếm cơ bản + chi tiết sản phẩm | Điểm chạm đầu tiên |
| Giỏ hàng (thêm/sửa/xóa) | Trái tim luồng mua |
| Checkout (địa chỉ, ship, coupon, tax, payment) | Nơi dồn toàn bộ độ phức tạp |
| COD | Đường thanh toán **luôn** hoạt động |
| Lịch sử đơn + chi tiết + hủy đơn | Bằng chứng đơn đã tạo |
| Admin: Orders + Products | Chứng minh vòng vận hành |

### 3.2 BUILT-FROZEN — giữ nguyên, không mở rộng

| Tính năng | Quyết định | Ghi chú |
|---|---|---|
| VNPay + Return + IPN + idempotency | Giữ code, đóng băng | Ẩn UI khi chưa cấu hình (T1) |
| Coupon | Đóng băng | Không thêm loại giảm giá mới |
| Review (verified purchase + duyệt) | Đóng băng | Không thêm reply/helpful vote |
| Profile + sổ địa chỉ | Đóng băng | Không thêm địa chỉ mặc định nâng cao |
| Shipping methods + Tax | Đóng băng | Không tích hợp GHN/GHTK |
| Multi-currency (display-only) | Đóng băng | Đã an toàn: 26 `<vc:money>`, lỗi API → fallback VND |
| Email (`LogEmailSender`) | Đóng băng | **Chỉ ghi log**, không gửi thật |
| Admin: Categories, Users/Roles, Coupons, Reviews, Shipping | Đóng băng | Làm lộ ra sidebar (T2) |
| Dashboard thống kê | Đóng băng | Không thêm chart/export |
| Dark mode, `/healthz` | Đóng băng | |

### 3.3 HIDE-OR-FIX — đang gây hại, phải xử lý trong đợt này

| # | Vấn đề | Cách xử lý | Task |
|---|---|---|---|
| 1 | Radio VNPay hiển thị dù chưa cấu hình → đơn tạo ra nhưng không trả được | Ẩn bằng config + guard server-side | **T1** |
| 2 | Sidebar admin thiếu 3 link → 3 tính năng P1 vô hình | Thêm link | **T2** |
| 3 | README mô tả sai (.NET 9, port, admin, email) | Sửa cho khớp | **T3** |

### 3.4 NEVER — không làm, không bàn lại

**P2 trong `docs/roadmap.md` (5 mục):** catalog nâng cao (sort/lọc giá/phân trang), wishlist, reporting/export, audit log, sản phẩm liên quan.

Lý do kỹ thuật — cả 5 đều có blast radius lớn **đúng vào golden path**:

| P2 | Chạm vào | Rủi ro |
|---|---|---|
| Catalog nâng cao | `HomeController`, query sản phẩm, view catalog | Sửa truy vấn nền trang chủ + danh mục → dễ vỡ D1 |
| Wishlist | Session/cart + bảng mới | Đụng state giỏ hàng → dễ vỡ D1 |
| Audit log | **Mọi action ghi** (cross-cutting) | Rải hook khắp controller → dễ vỡ D1 + D4 |
| Reporting/export | Query admin | Nặng DB, dễ timeout khi demo |
| Sản phẩm liên quan | Trang chi tiết + query | Chậm trang chi tiết |

**Khóa vĩnh viễn (ngoài P2):** MoMo/Stripe/gateway khác · SMTP/SendGrid thật · upload ảnh thật (S3/Cloudinary) · Hangfire/background jobs · Redis · CI/CD pipeline · multi-vendor · đa kho · loyalty point · gift card · subscription · chat/support · recommendation engine · tách microservices · GraphQL · app mobile · i18n đầy đủ · API vận chuyển thật · hóa đơn điện tử · refund/hoàn tiền từng phần.

---

## 4. Phát hiện đã kiểm chứng trong code

### 4.1 VNPay — điểm chết khi demo (mức cao nhất)

| Bằng chứng | Vị trí |
|---|---|
| `VnPay.TmnCode`, `HashSecret`, `ReturnUrl` đều rỗng | `ECommerce.Web/appsettings.json`, `appsettings.Production.json` |
| Radio VNPay render **vô điều kiện** | `Views/Cart/CheckoutPreview.cshtml:158-162` |
| Thiếu config → trả `Failure("...chưa được cấu hình...")` | `Services/Payments/VnPayGateway.cs:39-51` |
| Đơn **được tạo trước** khi gọi gateway; fail **không** hủy đơn | `Controllers/OrdersController.cs` (`Checkout`, ~dòng 125-180) |
| Không có nút "Thanh toán lại" | `Views/Orders/Details.cshtml` (không có match `Thanh toán lại`/`RetryPayment`) |
| JS toggle note **không** null-safe với `note` | `CheckoutPreview.cshtml:267,280` (`note.classList` gọi trực tiếp) |

**Kịch bản xấu:** chọn VNPay → đặt hàng → lỗi cấu hình → đơn "chờ thanh toán" không làm gì được nữa.
**Khắc phục:** T1 (ẩn UI + chặn server-side). **Không** sửa `VnPayGateway`, **không** thêm retry payment.

### 4.2 Admin sidebar thiếu 3 link

`Views/Shared/_AdminLayout.cshtml` chỉ có: Dashboard (79), Products (85), Categories (90), Orders (95), Users (102).
**Không có** Coupons / Reviews / ShippingMethods — chỉ lộ qua quick-link ở `Views/Admin/Index.cshtml:15,18,21`.
→ Đã đầu tư P1 nhưng người xem không biết tồn tại. Khắc phục: T2.

### 4.3 README sai so với thực tế

| Dòng | Hiện tại | Thực tế |
|---|---|---|
| 3 | "ASP.NET Core 9 MVC", "EF Core" | `net10.0`, EF Core 10.0.12 |
| 18 | `- [xtag:array][x] **Đánh giá sản phẩm**` | Dòng hỏng, phải là `- [x]` |
| 39 | "Email thông báo: Xác thực tài khoản, quên mật khẩu..." | `LogEmailSender` — chỉ ghi log |
| 45 | "ASP.NET Core MVC 9" | 10 |
| 79 | ".NET 9 SDK" + link `dotnet/9.0` | 10 |
| 99 | "`https://localhost:7xxx` hoặc `http://localhost:5xxx`" | `https://localhost:7133`, `http://localhost:5135` |
| Demo Live | "Admin account: Liên hệ" | Dev: `admin@ecommerce.local` / `Admin@12345` (chỉ Development — `DbInitializer.cs:37-40`) |
| 52, 128 | "Docker" | Đúng — `Dockerfile` tồn tại ở root |

Khắc phục: T3.

### 4.4 Điểm tích cực — giữ nguyên, đừng "cải tiến"

- Multi-currency **không** phải no-op: 26 chỗ `<vc:money>`, fallback VND an toàn (`CurrencyService.cs`).
- Identity lockout 5 lần / 5 phút (`Program.cs:80-82`).
- VNPay có idempotency + `FixedTimeEquals` + từ chối hash rỗng (`VnPayGateway.cs:85-146`).
- 266 unit test; `/healthz` 200.
- Rate limiting chỉ áp dụng production (`appsettings.Production.json`).

---

## 5. Golden path — bảo vệ bằng mọi giá

5 bước, ~3 phút. Mọi thay đổi phải chạy lại đúng 5 bước này trước khi commit.

1. **Khách:** trang chủ → tìm/lọc → chi tiết sản phẩm
2. **Khách:** thêm 2 sản phẩm vào giỏ → sửa số lượng
3. **Khách:** checkout → địa chỉ → áp coupon → chọn ship → **COD** → đặt hàng
4. **Khách:** lịch sử đơn → thấy đơn vừa tạo, trạng thái đúng
5. **Admin:** đăng nhập → xem đơn → đổi trạng thái → duyệt 1 review → thêm 1 sản phẩm

**Điều kiện đỗ:** không bước nào ra 500 / trang trắng / thông báo lỗi cấu hình.

---

## 6. Task thực thi chi tiết

### T0 — Baseline & chốt mốc (30')

**Làm:**
```powershell
dotnet test --nologo -v q
curl.exe -s -o NUL -w "%{http_code}`n" --max-time 60 https://ecommerce-demo-9phe.onrender.com/healthz
```
Chạy tay golden path §5, ghi lại kết quả từng bước.

**Sau khi T1–T5 xong mới tạo tag:**
```powershell
git tag -a v1.0-demo -m "Scope-locked demo build for CV/portfolio"
```

**Nghiệm thu:** 266/266 PASS, `/healthz` = 200, 5/5 bước OK.
**Lưu ý:** chưa có tag nào trong repo (`git tag` rỗng) → `v1.0-demo` là tag đầu tiên.

---

### T1 — Ẩn VNPay khi chưa cấu hình (45')

#### T1.1 — Thêm thuộc tính kiểm tra config

**File:** `ECommerce.Web/Models/Configuration/VnPayOptions.cs`
Thêm property (không đổi schema DB, không phải entity):

```csharp
/// <summary>Đủ cấu hình để khởi tạo giao dịch VNPay hay chưa.</summary>
public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(TmnCode) &&
    !string.IsNullOrWhiteSpace(HashSecret) &&
    !string.IsNullOrWhiteSpace(ReturnUrl);
```

> `BaseUrl` không cần check vì luôn có default trong `appsettings.json`.
> Không sửa logic trong `VnPayGateway.cs` — giữ nguyên các check riêng để phòng thủ nhiều lớp.

#### T1.2 — Ẩn radio ở view

**File:** `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`

1. Đầu file, thêm inject:
```cshtml
@inject Microsoft.Extensions.Options.IOptions<ECommerce.Web.Models.Configuration.VnPayOptions> VnPayConfig
```
2. Trong khối `<fieldset>` thanh toán, đặt biến đầu khối:
```cshtml
@{ var vnPayReady = VnPayConfig.Value.IsConfigured; }
```
3. Bọc **cả** `<label>` VNPay (dòng ~158-162) **và** `<div id="vnPayNote">` (dòng ~167) trong `@if (vnPayReady) { ... }`.
4. Sửa JS dòng ~280 thành null-safe:
```js
if (note) note.classList.toggle('hidden', !(vnPay && vnPay.checked));
```
> Bắt buộc: nếu chỉ bọc label mà bỏ sót `note`, `note` sẽ là `null` và JS ném lỗi → **vỡ toàn bộ script thanh toán**. Đây chính là loại lỗi im lặng cần tránh.
> `vnPay` đã null-safe sẵn (`vnPay && vnPay.checked`).

#### T1.3 — Guard server-side

**File:** `ECommerce.Web/Controllers/OrdersController.cs`, method `Checkout`, **trước** khi tạo đơn (trước dòng ~125 `_orderService...`):

```csharp
if (model.PaymentMethod == PaymentMethod.VnPay && !_vnPayOptions.IsConfigured)
{
    ModelState.AddModelError(nameof(model.PaymentMethod),
        "Thanh toán VNPay hiện chưa khả dụng. Vui lòng chọn thanh toán khi nhận hàng (COD).");
    _notyf.Error("VNPay hiện chưa khả dụng, vui lòng chọn COD.");
    await PopulateCheckoutAsync(model);
    return View("~/Views/Cart/CheckoutPreview.cshtml", model);
}
```

Cần inject `IOptions<VnPayOptions>` (hoặc dùng `VnPayOptions` đã đăng ký singleton ở `Program.cs:52` — **ưu tiên inject `VnPayOptions` trực tiếp** vì đã có sẵn `AddSingleton(sp => sp.GetRequiredService<IOptions<VnPayOptions>>().Value)`).

> ⚠️ **Lưu ý Unit Test khi thêm tham số constructor vào `OrdersController`:**
> `ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs` (dòng 58) có hàm khởi tạo `CreateController(...)` gọi trực tiếp `new OrdersController(...)`.
> Để tránh vỡ compilation của các test hiện có:
> - Thêm tham số với default null: `VnPayOptions? vnPayOptions = null` và gán fallback: `_vnPayOptions = vnPayOptions ?? new VnPayOptions();` (hoặc cập nhật đồng thời `CheckoutControllerTests.CreateController`).

Mục đích: chặn cả trường hợp form cũ / POST thủ công, đảm bảo **không bao giờ** tạo đơn VNPay khi chưa cấu hình.

#### T1.4 — Kiểm thử (khuyến nghị thực hiện)

**File:** `ECommerce.Web.Tests/`
1. Unit test logic `VnPayOptions.IsConfigured` (2-3 case):
   - Đủ 3 field → `true`
   - Thiếu `ReturnUrl` → `false`
   - Field toàn khoảng trắng → `false`
2. Controller test trong `CheckoutControllerTests.cs` (1 case):
   - `Checkout_WhenVnPaySelectedAndNotConfigured_DoesNotCreateOrderAndAddsError`: Giả lập POST VNPay khi `IsConfigured == false` → verify `_orderService.CreateOrderAsync` **không bao giờ** được gọi, model state có lỗi, trả về view checkout.

**Nghiệm thu T1:**
- Chạy app local với config rỗng: checkout **chỉ** thấy COD; console không có lỗi JS
- POST thủ công `PaymentMethod=VnPay`: bị chặn, **không** tạo đơn, hiện thông báo
- Đặt COD: vẫn chạy bình thường (golden path bước 3)
- `dotnet test` đạt 270/270 PASS (266 cũ + 3 test config + 1 test controller guard)

---

### T2 — Thêm 3 link vào admin sidebar (20')

**File:** `ECommerce.Web/Views/Shared/_AdminLayout.cshtml`

Chèn sau mục Orders (dòng ~95-101), theo đúng pattern class hiện có:

| Link | `asp-action` | `currentAction` prefix | Icon |
|---|---|---|---|
| Mã giảm giá | `Coupons` | `"Coupon"` | `fa-ticket-alt` |
| Đánh giá | `Reviews` | `"Review"` | `fa-star` |
| Vận chuyển | `ShippingMethods` | `"Shipping"` | `fa-truck` |

Mẫu (sao chép nguyên cấu trúc của mục Orders, chỉ đổi action/icon/nhãn):
```cshtml
<a asp-controller="Admin" asp-action="Coupons"
   class="flex items-center px-3.5 py-2.5 rounded-xl text-sm font-medium transition-colors @(currentAction.StartsWith("Coupon") ? "bg-primary text-white shadow-md shadow-indigo-600/30" : "text-slate-300 hover:bg-white/5 hover:text-white")">
    <i class="fas fa-ticket-alt w-5 mr-2"></i> Mã giảm giá
</a>
```

**Chỉ sửa presentation. Không đổi controller, không đổi route.**

**Nghiệm thu T2:** đăng nhập admin → thấy 6 mục sidebar → bấm từng mục không 404; mục đang active được highlight đúng.

---

### T3 — README trung thực (40')

**File:** `README.md` (root)

| # | Sửa gì | Thành |
|---|---|---|
| 1 | Dòng 18 `- [xtag:array][x]` | `- [x]` |
| 2 | Dòng 3, 45: "ASP.NET Core 9 MVC" / "MVC 9" | "ASP.NET Core MVC 10" (nhấn `net10.0`) |
| 3 | Dòng 79: ".NET 9 SDK" + link `dotnet/9.0` | ".NET 10 SDK" + `dotnet/10.0` |
| 4 | Dòng 99: `7xxx/5xxx` | `https://localhost:7133` và `http://localhost:5135` |
| 5 | Demo Live — "Admin account: Liên hệ" | Dev: `admin@ecommerce.local` / `Admin@12345` (chỉ Development). Production: cần env `SeedAdmin__Password` (app **fail-fast** nếu thiếu — `DbInitializer.cs:46-50`) |
| 6 | Dòng 39 Email | Ghi rõ: "Email trong bản demo dùng `LogEmailSender` — nội dung (link xác thực / reset) được **ghi vào log**, không gửi SMTP thật." |
| 7 | Mục VNPay | "Đã tích hợp VNPay (...). Mặc định **tắt** khi chưa cấu hình `VnPay:TmnCode`/`HashSecret`/`ReturnUrl` — bản demo dùng COD." |
| 8 | Thêm mục **"Giới hạn đã biết"** | ảnh lưu trên disk tạm của Render free (mất khi redeploy) · cold start · không background jobs · không caching · không CI/CD · rate limit chỉ bật ở production · P2 chưa làm (đóng băng có chủ đích) |
| 9 | Kiểm lại | mọi port, phiên bản, link, số test 266 |

**Nghiệm thu T3:** không còn `xtag`, `9.0`, `7xxx`, `5xxx`; mọi khẳng định đều kiểm chứng được bằng code/config; link tương đối và link ngoài đều mở được.

---

### T4 — Ghi luật khóa vào repo (20')

**File mới:** `docs/scope-lock.md`

Nội dung tối thiểu:
1. Định nghĩa "đủ" D1–D5 (§1)
2. Bảng phân loại CORE / BUILT-FROZEN / HIDE-OR-FIX / NEVER (§3)
3. Quy tắc khóa cứng (§7)
4. Golden path 5 bước (§5)
5. Ngày chốt + baseline commit + tag `v1.0-demo`

**Cập nhật kèm:**
- `docs/README.md` — thêm dòng vào bảng "Đọc gì khi nào": *"Phạm vi đã khóa / không làm gì nữa" → `scope-lock.md`*
- `docs/roadmap.md` — ghi chú P2: **"Đóng băng có chủ đích — xem `docs/scope-lock.md`. Không lập plan."**

**Nghiệm thu T4:** link trong `docs/README.md` trỏ đúng file; P2 trong roadmap không còn bị hiểu là "sẽ làm".

---

### T5 — Kiểm chứng cuối + tag (45')

```powershell
dotnet test --nologo -v q
node .gitnexus/run.cjs detect-changes --scope all
git status --short
```

Checklist:
- [ ] 266/266 (hoặc 270/270 nếu thêm test T1.4) PASS
- [ ] Golden path 5/5 bước OK trên local
- [ ] Golden path 3/3 bước chính OK trên Render (sau khi deploy)
- [ ] `/healthz` = 200
- [ ] Checkout chỉ hiện COD khi config rỗng
- [ ] Admin sidebar đủ 6 mục, không 404
- [ ] README không còn thông tin sai
- [ ] `detect-changes` không có HIGH/CRITICAL
- [ ] Tag `v1.0-demo` đã tạo

**Ảnh chụp (portfolio):** trang chủ · chi tiết sản phẩm · giỏ hàng · checkout · đơn hàng · admin dashboard. Lưu vào `screenshots/` và nhúng vào README (thay phần placeholder).

---

### T6 — Dọn dẹp (tùy chọn, ngoài scope chính)

- CRLF noise: `.gitattributes` đã có nhưng **chưa** chạy `git add --renormalize .` → diff sẽ rất lớn. **Khuyến nghị: KHÔNG chạy trong đợt này.** Làm thành commit riêng khi rảnh, hoặc bỏ qua vĩnh viễn (không ảnh hưởng demo).
- `opencode.json` và các thay đổi pre-existing khác: **không** commit chung với đợt này.

---

## 7. Quy tắc khóa cứng (áp dụng từ ngày chốt)

### Được phép
1. Sửa bug có repro rõ ràng
2. Vá cổng hiển thị (ẩn tính năng chưa cấu hình)
3. Sửa tài liệu cho khớp sự thật
4. Thêm ảnh/screenshot, sửa copy
5. Nâng cấp bảo mật (package patch)

### Cấm
- ❌ Thêm entity/bảng mới
- ❌ Thêm controller/area/trang admin mới
- ❌ Thêm NuGet package mới (trừ security patch)
- ❌ Đổi schema thanh toán hoặc luồng trạng thái đơn
- ❌ Đổi query nền của catalog/trang chủ
- ❌ Trộn nhiều mục đích trong một commit
- ❌ Làm P2 trong repo này (nếu vẫn muốn học → nhánh `experimental/p2`, **không merge**)

### Định nghĩa "xong" cho mọi thay đổi
```
dotnet test                                    → 266/266 PASS
5 bước golden path (chạy tay)                  → không lỗi
node .gitnexus/run.cjs detect-changes --scope all → không HIGH/CRITICAL
```

### Commit hygiene
- Commit nhỏ, một mục đích, prefix `fix:` / `docs:` / `chore:`
- `git add <đường-dẫn-cụ-thể>`, **không** `git add -A`
- Chạy `detect-changes` **trước** mỗi commit (theo `AGENTS.md`)

---

## 8. Non-goals (tuyên bố rõ)

- Không làm P2.
- Không làm "production-ready" (không email thật, không lưu ảnh bền vững, không CI/CD, không monitoring).
- Không tối ưu hiệu năng, không refactor kiến trúc.
- Không thêm trang, không thêm bảng, không thêm package.
- Không mở rộng admin portal ngoài 3 link sidebar.
- Không viết test mới trừ khi cần cho bug fix / guard (T1.4 là ngoại lệ có kiểm soát).
- Không thêm nút "Thanh toán lại" (đã hủy do chọn 1A).

**Định vị:** đây là **portfolio piece**, không phải sản phẩm vận hành. "Đủ" = chứng minh năng lực thiết kế + triển khai một luồng thương mại điện tử hoàn chỉnh, không cạnh tranh với Shopee.

---

## 9. Rủi ro & cách lùi

| Rủi ro | Khả năng | Dấu hiệu | Cách lùi |
|---|---|---|---|
| Bọc `@if` làm hỏng JS checkout | Trung bình | Console lỗi `Cannot read properties of null`, radio không highlight | Đảm bảo guard `if (note)` (T1.2 bước 4); test cả COD |
| `IsConfigured` chặn nhầm khi config đọc từ env | Thấp | Production không hiện VNPay dù đã set env | Kiểm tra `ReturnUrl` cũng phải set; log giá trị khi debug |
| Thêm link sidebar làm vỡ layout mobile | Thấp | Sidebar tràn, phải scroll | Sidebar đã scroll sẵn; kiểm ở 375px |
| README sửa sai tiếp | Thấp | Claim không kiểm chứng được | Mỗi dòng sửa phải dẫn được tới file/config thật |
| Renormalize CRLF gây diff khổng lồ | Cao nếu chạy | `git status` hàng trăm file | **Không chạy** trong đợt này (T6) |

**Nguyên tắc lùi:** nếu một task không đạt nghiệm thu trong 2 lần thử → `git checkout -- <file>`, ghi lại vào report, chuyển sang task khác. Không "cố thêm tí nữa".

---

## 10. Checklist nghiệm thu toàn plan

- [ ] T0: baseline xanh (266/266, `/healthz` 200, golden path 5/5)
- [ ] T1: VNPay ẩn khi chưa cấu hình; POST thẳng bị chặn; COD không đổi hành vi
- [ ] T2: sidebar admin 6 mục, không 404, highlight đúng
- [ ] T3: README không còn `xtag`/`9.0`/`7xxx`/`5xxx`; có mục "Giới hạn đã biết"
- [ ] T4: `docs/scope-lock.md` tồn tại, được link từ `docs/README.md`, roadmap ghi P2 = đóng băng
- [ ] T5: test xanh sau toàn bộ thay đổi; screenshot; tag `v1.0-demo`
- [ ] Không file nào ngoài phạm vi bị sửa (`git status` sạch ngoài các file dự kiến)
- [ ] Báo cáo kiểm chứng: `docs/superpowers/reports/2026-10-28-scope-lock.md`

---

## 11. Thứ tự thực thi đề xuất

```
T0 (baseline)  →  T1 (VNPay)  →  T2 (sidebar)  →  T3 (README)  →  T4 (scope-lock doc)  →  T5 (verify + tag)
```

T1 làm trước vì nó là rủi ro demo cao nhất. T3/T4 làm sau cùng vì phụ thuộc vào kết quả T1/T2 (README phải mô tả đúng hành vi sau khi sửa).
