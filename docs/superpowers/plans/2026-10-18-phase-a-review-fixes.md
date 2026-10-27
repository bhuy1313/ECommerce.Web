# Phase A Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa toàn bộ phát hiện của self-review Phase A (1 HIGH, 1 MEDIUM, 6 LOW) trên nhánh `main` sau commit `8eb0ef3`, không thay đổi bất kỳ hành vi nào khác.

**Architecture:** Bốn thay đổi có tính cấu trúc, phần còn lại là vệ sinh test/view. (1) `HandleCallbackAsync` trả về kết quả **có cấu trúc** (`PaymentCallbackOutcome`) thay cho tuple + dò chuỗi con trong `Message` — đây là refactor thuần, không đổi hành vi, và là chỗ để Task 3 gắn nhánh mới. (2) Thêm `PaymentStatus.RefundPending` để phân biệt "đã huỷ và đang **nợ** hoàn tiền" với "đã hoàn tiền". (3) Callback hợp lệ cho đơn **đã huỷ** không được hồi sinh đơn. (4) Validate `PaymentMethod` có thông báo hiển thị được + `id` radio duy nhất.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server dev / SQLite prod), xUnit 2.9.3 + Moq 4.20.72 + EF InMemory/Sqlite, Razor + Tailwind CDN + FontAwesome, ToastNotification (notyf).

**Spec:** §0 của tài liệu này — kết quả self-review Phase A (vùng commit `44a11f6..8eb0ef3`), hợp nhất với `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` (mục Review Focus #5).

## Global Constraints

- Target framework `net10.0`. Test project `ECommerce.Web.Tests`.
- Ngôn ngữ UI tiếng Việt; tiền tệ VND qua `<vc:money amount="..." />` / `ICurrencyService`. Không tự định dạng tiền bằng tay.
- Mọi action ghi (POST) phải có `[ValidateAntiForgeryToken]` và `[EnableRateLimiting("mutating")]`.
- **Không thêm EF migration.** `PaymentStatus` được lưu dạng `int`, thêm member enum **không** đổi schema. Nếu bất kỳ bước nào chạm schema, dừng lại và báo cáo thay vì tự sinh migration.
- Giữ build 0 warning: dùng `async Task` + `await`, không `.GetAwaiter().GetResult()` (xUnit1031).
- Sau mỗi task, toàn bộ test phải xanh (114 test hiện có, trừ những assertion được thay thế **có chủ đích** ở Task 1 và Task 2).
- Lệnh verify chuẩn: `dotnet build --no-incremental` và `dotnet test`.
- `git add` theo **đường dẫn file cụ thể**. Repo đặt `core.autocrlf=true` và không có `.gitattributes`, nên `git status` báo hàng chục file " M" giả (nhiễu EOL); kiểm tra `git diff --cached --numstat` trước khi commit.
- Commit nhỏ theo từng task, prefix `fix:` / `refactor:` / `test:` theo phong cách repo.
- Không đụng tới 3 file ngoài phạm vi đang dirty: `docs/roadmap.md`, `opencode.json`, `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md`.

## Review Focus

Các lớp đầu vào / chế độ lỗi mà bản review nêu ra nhưng **chưa** test nào phủ — mỗi dòng được gắn test ở task sở hữu code:

1. Callback hợp lệ cho đơn **đã huỷ** — đơn KHÔNG hồi sinh, tồn kho KHÔNG bị trừ lại, tiền được ghi nhận là **nợ** hoàn (`RefundPending`), RspCode `00` để VNPay ngừng retry. (Task 3)
2. Callback **lặp lại** sau ca (1) — vẫn idempotent: không ghi lần hai, `Outcome` lần hai là `AlreadyProcessed`, đơn vẫn `Cancelled` + `RefundPending`. (Task 3)
3. POST Checkout với `PaymentMethod` **ngoài enum** (ví dụ `999`) — render lại form kèm **lỗi hiển thị được**, không tạo đơn, không trừ kho. (Task 4)
4. `StartPaymentAsync` gọi **hai lần** cho cùng đơn COD (ref tất định) — không `DbUpdateException`, không sinh giao dịch thứ hai. (Task 7)
5. Huỷ đơn **đã thanh toán** — chuyển `RefundPending` (không phải `Refunded`), badge không khẳng định một khoản hoàn tiền chưa xảy ra, ở cả storefront lẫn admin. (Task 2)

---

## §0 Findings và quyết định

### Findings (nguồn: self-review Phase A)

| # | Mức | Nơi | Vấn đề |
|---|-----|-----|--------|
| H1 | HIGH | `ECommerce.Web/Services/Payments/PaymentService.cs:153-154` | Callback hợp lệ **hồi sinh** đơn đã huỷ (`Paid` + `Processing`) dù tồn kho đã được hoàn lúc huỷ ⇒ bán vượt tồn, tiền đã thu không có đường hoàn. `pdg_query(controls)` xác nhận block ghi tiền (dòng 144) **không** có guard nào theo `order.Status`. Reproduce được: đơn `12013`. |
| M1 | MEDIUM | `Views/Orders/Details.cshtml:81-85` + `Services/OrderService.cs:191-194` | Huỷ đơn **đã thanh toán** đặt `PaymentStatus = Refunded` ⇒ badge "Đã hoàn tiền" trong khi **không có** lệnh hoàn tiền nào được gọi. |
| L1 | LOW | `Controllers/PaymentController.cs:34-39` | `RspCode` suy ra bằng cách dò **chuỗi con** trong `Message` của `PaymentService` ⇒ coupling theo câu chữ. |
| L2 | LOW | `Views/Cart/CheckoutPreview.cshtml:60-78` | Hai radio sinh **trùng `id="PaymentMethod"`** (quan sát `id_count=2`); `<label asp-for>` trỏ sai radio; thiếu `fieldset`/`legend`. |
| L3 | LOW | `Views/Cart/CheckoutPreview.cshtml:66-84` + `Models/ViewModels/CheckoutViewModel.cs:28-29` | Lỗi validate phương thức thanh toán **vô hình**: `PaymentMethod=7` → re-render 200, không tạo đơn, nhưng **không** có thông báo nào ở trường đó. |
| L4 | LOW | `Views/Admin/Products.cshtml:96`, `Views/Admin/ProductForm.cshtml:78,111` | Hardcode `/img/product-placeholder.svg` thay vì `ProductImage.Placeholder` (4 nơi nếu tính cả const) ⇒ lệch chuẩn với storefront. |
| L5 | LOW | `ECommerce.Web.Tests/Helpers/VnPaySignatureHelper.cs` + `Tests/Services/VnPayGatewayTests.cs:92-95` | Test so chữ ký production với **bản sao cùng thuật toán** ⇒ không có golden vector; một cách hiểu sai spec (thứ tự sort / encode / thuật toán) giữ mọi test xanh. |
| L6 | LOW | `Controllers/OrdersController.cs:108-121` + `Services/Payments/CodPaymentGateway.cs:17` | Gọi lại `StartPaymentAsync` cho cùng đơn COD → ref tất định `COD-{orderId}` trùng unique index → `DbUpdateException` → **500** (không `try/catch`). |

**Ngoài phạm vi** (đã kiểm và không sửa): làm tròn `vnp_Amount`, `FixedTimeEquals` + chặn `HashSecret` rỗng, `PaymentController` không `[Authorize]`/antiforgery (là chủ ý, có HMAC), scope IDOR của `Orders/Details`, XSS ảnh admin.

### Quyết định (my calls — bác được thì nói, tôi sửa kế hoạch)

- **D1 — Đơn đã huỷ + tiền về ⇒ KHÔNG hồi sinh đơn.** Giữ `Status = Cancelled`, đặt `PaymentStatus = RefundPending` (đang nợ hoàn tiền), transaction = `Succeeded` (sự thật: đã thu tiền — cũng để đảm bảo idempotent), log `LogError` với marker rõ, trả `Outcome = PaidButCancelled`. **RspCode `00`** vì ta *đã* xử lý xong: trả mã retry sẽ khiến VNPay gọi lại 10 lần vô ích.
- **D2 — `RefundPending` là trạng thái thật.** `PaymentStatus` chỉ mang giá trị `Refunded` khi hoàn tiền **đã xảy ra**. Hoàn tiền thủ công do admin thực hiện qua `AdminController.UpdatePaymentStatus` (`AdminController.cs:430`) — không viết quy trình hoàn tiền tự động (ngoài phạm vi Phase A, không có tích hợp hoàn tiền của VNPay).
- **D3 — Kết quả callback có cấu trúc.** Thay tuple `(bool, string, int?)` bằng `PaymentCallbackHandlingResult` + enum `PaymentCallbackOutcome`; `PaymentController` map `Outcome → RspCode` bằng `switch` tường minh. Tên type **không** được là `PaymentCallbackResult` — tên đó đã dùng cho kết quả verify cấp gateway (`Services/Payments/PaymentModels.cs:26`).
- **D4 — Validate `PaymentMethod` chỉ để *hiển thị* lỗi.** Model binding **đã** từ chối `999` (probe: re-render 200, không tạo đơn); cái thiếu là thông báo ở trường. Thêm `[EnumDataType]` để có message đúng chỗ và thêm `asp-validation-for`.
- **D5 — `StartPaymentAsync` idempotent theo `TransactionRef`.** Bỏ qua bước chèn khi đã tồn tại transaction cùng `(Provider, TransactionRef)`. Cách này giữ nguyên ngữ nghĩa: COD tái dùng (ref tất định), VNPay vẫn tạo lượt mới (mỗi lượt một ref/một URL mới).
- **D6 — Golden vector chỉ ghim phần *chắc chắn* đúng.** Ghim **canonical string** (sort + encode + bỏ value rỗng) là nơi rủi ro spec thật sự nằm, vì nó độc lập với hàm băm. **Residual (ghi lại, không sửa):** chỉ round-trip sandbox VNPay thật mới chứng minh được end-to-end.

---

## Task 1: Kết quả callback có cấu trúc (refactor)

**Files:**
- Modify: `ECommerce.Web/Services/Payments/PaymentModels.cs` (thêm type mới; **không** đụng `PaymentCallbackResult` hiện có)
- Modify: `ECommerce.Web/Services/Payments/IPaymentService.cs:11`
- Modify: `ECommerce.Web/Services/Payments/PaymentService.cs` (`HandleCallbackAsync`, dòng 80-171)
- Modify: `ECommerce.Web/Controllers/PaymentController.cs` (dòng 34-39, 58, 85, 109-120)
- Test: `ECommerce.Web.Tests/Services/PaymentServiceTests.cs`
- Test: `ECommerce.Web.Tests/Controllers/PaymentControllerTests.cs`

**Interfaces:**
- Consumes: không.
- Produces:
  - `enum PaymentCallbackOutcome` (namespace `ECommerce.Web.Services.Payments`): `Confirmed, AlreadyProcessed, PaidButCancelled, OrderNotFound, TransactionNotFound, InvalidSignature, AmountMismatch, PaymentFailed, GatewayHasNoCallback, ProviderUnsupported, GatewayError`
  - `class PaymentCallbackHandlingResult { bool Success {get;set;} PaymentCallbackOutcome Outcome {get;set;} string Message {get;set;} = string.Empty; int? OrderId {get;set;} }` — dùng `class` + `{ get; set; }` cho khớp phong cách `PaymentModels.cs`, không positional record.
  - `IPaymentService.HandleCallbackAsync(string provider, IQueryCollection query) -> Task<PaymentCallbackHandlingResult>`

Ánh xạ 1-1 với hành vi hiện tại (Task 1 **không** đổi hành vi; `PaidButCancelled` chưa được sinh ra ở task này):

| Nhánh hiện tại | Outcome |
|---|---|
| `gateway == null` (dòng 83) | `ProviderUnsupported` |
| `NotSupportedException` (dòng 93) | `GatewayHasNoCallback` |
| `!callback.IsValidSignature` (dòng 98) | `InvalidSignature` |
| `transaction == null` (dòng 108) | `TransactionNotFound` |
| `transaction.Status == Succeeded` (dòng 114) | `AlreadyProcessed` |
| `!callback.Success` (dòng 119) | `PaymentFailed` |
| `order == null` (dòng 130) | `OrderNotFound` |
| `callback.Amount != order.TotalAmount` (dòng 135) | `AmountMismatch` |
| thành công (dòng 162) | `Confirmed` |
| `catch` (dòng 164) | `GatewayError` |

- [ ] **Step 1: Đổi sang kiểu mới, giữ nguyên hành vi**

Sửa theo thứ tự để `dotnet build` báo lỗi ở mọi call site cần sửa (RED biên dịch — đúng mẫu đã dùng ở task A10):

1. Thêm `PaymentCallbackOutcome` + `PaymentCallbackHandlingResult` vào `PaymentModels.cs`.
2. Đổi chữ ký `IPaymentService.HandleCallbackAsync`.
3. Trong `PaymentService.HandleCallbackAsync`: thay mỗi `return (false, "...", null)` bằng `return new PaymentCallbackHandlingResult { Success = …, Outcome = …, Message = …, OrderId = … }`. Câu chữ `Message` **giữ nguyên** (U+1EF1 v.v. — chép y bản cũ).
4. Trong `PaymentController`: thay bảng `RspCodeByMessage` + `ResolveRspCode` bằng một `switch` trên `result.Outcome` (dòng 34-39 và 109-120 bị xoá):
   - `Confirmed`, `AlreadyProcessed`, `PaidButCancelled` → `RspSuccess` (`"00"`)
   - `OrderNotFound`, `TransactionNotFound` → `RspOrderNotFound` (`"01"`)
   - `InvalidSignature` → `RspInvalidChecksum` (`"97"`)
   - `AmountMismatch` → `RspInvalidAmount` (`"04"`)
   - `ProviderUnsupported`, `GatewayHasNoCallback`, `PaymentFailed`, `GatewayError` → `RspUnknown` (`"99"`)
   Giữ nguyên comment giải thích nhóm RspCode (dòng 16-20) — nó vẫn đúng.
5. `Return()` dòng 58: `var result = await _paymentService.HandleCallbackAsync(...)`; điều kiện vào nhánh thành công là `result.Success && result.OrderId.HasValue && result.Outcome != PaymentCallbackOutcome.PaidButCancelled` (nhánh `PaidButCancelled` sẽ được Task 3 hoàn thiện; ở task này chỉ cần biên dịch và **không** đổi hành vi vì outcome đó chưa xuất hiện).

- [ ] **Step 2: Chạy build để xác nhận lỗi biên dịch ở các call site còn lại**

Run: `dotnet build --no-incremental`
Expected: FAIL, `CS0246`/`CS1061` ở `PaymentServiceTests.cs` (destructure tuple) và `PaymentControllerTests.cs` (`ServiceReturning`).

- [ ] **Step 3: Cập nhật test — thay assert theo câu chữ bằng assert theo `Outcome`**

Trong `PaymentServiceTests.cs`:
- Mọi `var (success, message, orderId) = await _service.HandleCallbackAsync(...)` → `var result = await _service.HandleCallbackAsync(...)`, rồi `result.Success` / `result.OrderId`.
- Thay coupling câu chữ bằng enum — đây chính là fix L1:
  - `HandleCallback_WhenInvalidSignature_...` (dòng 114 `Assert.Contains("Chữ ký", message)`) → `Assert.Equal(PaymentCallbackOutcome.InvalidSignature, result.Outcome)`
  - `HandleCallback_WhenAmountMismatch_Rejects` (dòng 137) → `Assert.Equal(PaymentCallbackOutcome.AmountMismatch, result.Outcome)`
  - `HandleCallback_WhenTransactionUnknown_Rejects` (dòng 159) → `Assert.Equal(PaymentCallbackOutcome.TransactionNotFound, result.Outcome)`
  - `HandleCallback_WhenProviderUnknown_Rejects` → `Assert.Equal(PaymentCallbackOutcome.ProviderUnsupported, result.Outcome)`
  - `HandleCallback_WhenGatewayHasNoCallback_Rejects` → `Assert.Equal(PaymentCallbackOutcome.GatewayHasNoCallback, result.Outcome)`
  - `HandleCallback_WhenValid_...` → `Assert.Equal(PaymentCallbackOutcome.Confirmed, result.Outcome)`
  - `HandleCallback_WhenCalledTwice_...` → lần hai `Assert.Equal(PaymentCallbackOutcome.AlreadyProcessed, second.Outcome)`
- Giữ các assert `Assert.False(string.IsNullOrWhiteSpace(...))` trên `Message` ở những test chỉ cần "có thông báo".

Trong `PaymentControllerTests.cs`:
- `ServiceReturning(bool, string, int?)` → `ServiceReturning(PaymentCallbackOutcome outcome, string message, int? orderId)`; `Success` suy ra từ outcome: `outcome is Confirmed or AlreadyProcessed or PaidButCancelled`.
- Các test IPN hiện dựng kịch bản bằng **câu chữ** → chuyển sang **outcome**:
  - `Ipn_WhenCallbackSucceeds_ReturnsRspCode00` → `Confirmed`
  - `Ipn_WhenSignatureInvalid_ReturnsRspCode97` → `InvalidSignature`
  - `Ipn_WhenOrderNotFound_ReturnsRspCode01` → `TransactionNotFound` (và thêm một `[Theory]`/Fact cho `OrderNotFound` → `"01"`)
  - `Ipn_WhenAmountMismatch_ReturnsRspCode04` → `AmountMismatch`
  - `Ipn_WhenOutcomeIsUnrecognised_ReturnsRspCode99` → đổi tên thành `Ipn_WhenPaymentFailed_ReturnsRspCode99` với `PaymentFailed` (giữ nguyên ý nghĩa: outcome không thuộc nhóm VNPay ngừng retry → `"99"`)
- `Return_WhenCallbackFails_RendersResultViewWithFailure` → `InvalidSignature`.

- [ ] **Step 4: Chạy test, xác nhận đúng 114 PASS**

Run: `dotnet test`
Expected: PASS, tổng số test **không đổi** (114). Nếu số test giảm, nghĩa là một test đã bị xoá thay vì sửa — hoàn tác và làm lại.

- [ ] **Step 5: Mutation check (bắt buộc với refactor không có test mới)**

Đổi tạm một ánh xạ, ví dụ `AmountMismatch` → `RspOrderNotFound`, chạy `dotnet test --filter "FullyQualifiedName~PaymentControllerTests"`, xác nhận test đỏ, rồi **restore** và chạy lại xanh.

- [ ] **Step 6: Commit**

```bash
git add ECommerce.Web/Services/Payments/PaymentModels.cs ECommerce.Web/Services/Payments/IPaymentService.cs ECommerce.Web/Services/Payments/PaymentService.cs ECommerce.Web/Controllers/PaymentController.cs ECommerce.Web.Tests/Services/PaymentServiceTests.cs ECommerce.Web.Tests/Controllers/PaymentControllerTests.cs
git commit -m "refactor: return structured callback outcome instead of message sniffing"
```

---

## Task 2: `PaymentStatus.RefundPending` — đừng nói dối về hoàn tiền (M1)

**Files:**
- Modify: `ECommerce.Web/Models/Entities/Enums/PaymentStatus.cs`
- Modify: `ECommerce.Web/Services/OrderService.cs:190-194`
- Modify: `ECommerce.Web/Views/Orders/Details.cshtml:74-91` và `:95-103`
- Modify: `ECommerce.Web/Views/Admin/OrderDetails.cshtml:167-178`
- Modify: `ECommerce.Web/Views/Admin/Orders.cshtml:128-139`
- Test: `ECommerce.Web.Tests/Services/OrderServiceTests.cs:232-267`

**Interfaces:**
- Consumes: `PaymentStatus.Paid` (đã có).
- Produces: `PaymentStatus.RefundPending = 4`; `CancelOrderAsync` đặt `RefundPending` (thay vì `Refunded`) khi đơn đang ở `Paid`.

- [ ] **Step 1: Sửa test hiện có thành thất bại (RED)**

Trong `OrderServiceTests.cs`: đổi tên `CancelOrderAsync_WhenOrderPaid_SetsRefunded` (dòng 232) → `CancelOrderAsync_WhenOrderPaid_SetsRefundPending`, và dòng 260 `Assert.Equal(PaymentStatus.Refunded, updatedOrder.PaymentStatus)` → `Assert.Equal(PaymentStatus.RefundPending, updatedOrder.PaymentStatus)`.

- [ ] **Step 2: Chạy test xác nhận đỏ vì enum chưa có member**

Run: `dotnet test --filter "FullyQualifiedName~CancelOrderAsync_WhenOrderPaid"`
Expected: FAIL biên dịch — `'PaymentStatus' does not contain a definition for 'RefundPending'`.

- [ ] **Step 3: Thêm member enum**

`PaymentStatus.cs` → thêm `RefundPending = 4,` sau `Refunded = 3,` kèm doc-comment một dòng: *"Đã thu tiền nhưng khoản hoàn chưa được thực hiện (đơn bị huỷ sau khi thanh toán). Chỉ chuyển sang `Refunded` khi admin xác nhận đã hoàn tiền."*

Không cần migration: `PaymentStatus` là cột `int`.

- [ ] **Step 4: Đổi `CancelOrderAsync` sang `RefundPending`**

`OrderService.cs:191-194`: giữ điều kiện `order.PaymentStatus == PaymentStatus.Paid`, đổi giá trị gán thành `PaymentStatus.RefundPending`.

- [ ] **Step 5: Chạy test xác nhận xanh**

Run: `dotnet test --filter "FullyQualifiedName~CancelOrderAsync"`
Expected: PASS toàn bộ nhóm `CancelOrderAsync_*`.

- [ ] **Step 6: Cập nhật badge ở cả ba view**

Quan trọng: hai switch trong Admin **không có `default`** (member mới sẽ render ra *rỗng*), còn switch ở `Orders/Details.cshtml` **có `default`** (member mới sẽ rơi nhầm vào "Chưa thanh toán"). Cả ba đều phải thêm case tường minh:

- `Views/Orders/Details.cshtml` — thêm `case PaymentStatus.RefundPending:` với badge amber:
  `<i class="fas fa-hourglass-half mr-1.5"></i> Chờ hoàn tiền`
  Dùng màu amber (`bg-amber-50 text-amber-700 border-amber-200`) — **khác** màu của `Paid` (emerald) và `Refunded` (gray) để badge không thể bị đọc lẫn.
- `Views/Admin/OrderDetails.cshtml:178` — thêm `case …RefundPending:` (trước `Refunded`) với `text-amber-700 bg-amber-50 border-amber-200` và nhãn `Chờ hoàn tiền (RefundPending)`.
- `Views/Admin/Orders.cshtml:139` — thêm `case …RefundPending:` với nhãn `Chờ hoàn tiền`.

Không cần sửa `<select>` đổi trạng thái ở `Admin/OrderDetails.cshtml:189`: nó sinh từ `Enum.GetValues(typeof(PaymentStatus))` nên member mới tự xuất hiện và admin dùng chính form đó để đánh dấu `Refunded` sau khi hoàn tiền xong (D2).

- [ ] **Step 7: Làm banner đơn đã huỷ nói đúng sự thật**

`Views/Orders/Details.cshtml:95-103`: banner hiện chỉ nói "đã hoàn trả tồn kho". Thêm nhánh khi `Model.PaymentStatus == PaymentStatus.RefundPending` để nói rõ đang chờ hoàn tiền, ví dụ nội dung: *"Đơn hàng đã hủy sau khi thanh toán: tồn kho đã được hoàn lại, khoản thanh toán đang chờ được hoàn tiền thủ công."* (giữ nguyên cấu trúc `div`/class hiện có).

- [ ] **Step 8: Chạy toàn bộ test + build**

Run: `dotnet build --no-incremental` rồi `dotnet test`
Expected: 0 warning, toàn bộ PASS (114).

- [ ] **Step 9: Commit**

```bash
git add ECommerce.Web/Models/Entities/Enums/PaymentStatus.cs ECommerce.Web/Services/OrderService.cs ECommerce.Web/Views/Orders/Details.cshtml ECommerce.Web/Views/Admin/OrderDetails.cshtml ECommerce.Web/Views/Admin/Orders.cshtml ECommerce.Web.Tests/Services/OrderServiceTests.cs
git commit -m "fix: mark cancelled-and-paid orders as awaiting refund instead of refunded"
```

---

## Task 3: Callback không hồi sinh đơn đã huỷ (H1)

**Files:**
- Modify: `ECommerce.Web/Services/Payments/PaymentService.cs` (chèn giữa dòng 141 và 143)
- Modify: `ECommerce.Web/Controllers/PaymentController.cs` (`Return`, dòng ~58-74)
- Test: `ECommerce.Web.Tests/Services/PaymentServiceTests.cs`
- Test: `ECommerce.Web.Tests/Controllers/PaymentControllerTests.cs`

**Interfaces:**
- Consumes: `PaymentCallbackOutcome.PaidButCancelled` (Task 1), `PaymentStatus.RefundPending` (Task 2).
- Produces: `HandleCallbackAsync` trả `Outcome = PaidButCancelled`, `Success = true`, `OrderId = order.Id` khi đơn đã huỷ; `PaymentController.Return` hiển thị thông báo trung tính cho outcome này.

- [ ] **Step 1: Viết hai test thất bại**

Thêm vào `PaymentServiceTests.cs` (dùng lại `SeedOrderAsync` — helper đã nhận `(PaymentStatus paymentStatus, OrderStatus status)`):

```csharp
[Fact]
public async Task HandleCallback_WhenOrderCancelled_DoesNotResurrectOrderAndFlagsRefund()
{
    var order = await SeedOrderAsync(status: OrderStatus.Cancelled);
    var transaction = await SeedTransactionAsync(order.Id);
    var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

    var result = await _service.HandleCallbackAsync("VnPay", query);

    Assert.True(result.Success);
    Assert.Equal(PaymentCallbackOutcome.PaidButCancelled, result.Outcome);
    Assert.Equal(order.Id, result.OrderId);

    var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
    Assert.Equal(OrderStatus.Cancelled, updatedOrder.Status);
    Assert.Equal(PaymentStatus.RefundPending, updatedOrder.PaymentStatus);

    var updatedTransaction = await _db.PaymentTransactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id);
    Assert.Equal(PaymentTransactionStatus.Succeeded, updatedTransaction.Status);
}

[Fact]
public async Task HandleCallback_WhenOrderCancelledAndReplayed_StaysIdempotent()
{
    var order = await SeedOrderAsync(status: OrderStatus.Cancelled);
    await SeedTransactionAsync(order.Id);
    var query = VnPaySignatureHelper.BuildCallbackQuery(_options, TxnRef, OrderTotal);

    var first = await _service.HandleCallbackAsync("VnPay", query);
    var second = await _service.HandleCallbackAsync("VnPay", query);

    Assert.Equal(PaymentCallbackOutcome.PaidButCancelled, first.Outcome);
    Assert.Equal(PaymentCallbackOutcome.AlreadyProcessed, second.Outcome);

    var updatedOrder = await _db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
    Assert.Equal(OrderStatus.Cancelled, updatedOrder.Status);
    Assert.Equal(PaymentStatus.RefundPending, updatedOrder.PaymentStatus);
    Assert.Equal(1, await _db.PaymentTransactions.CountAsync(t => t.Status == PaymentTransactionStatus.Succeeded));
}
```

Thêm vào `PaymentControllerTests.cs`:

```csharp
[Fact]
public async Task Ipn_WhenPaidButCancelled_ReturnsRspCode00()
{
    // Ta ĐÃ xử lý callback (đã thu tiền, đã đánh dấu nợ hoàn). Trả mã retry sẽ khiến
    // VNPay gọi lại 10 lần vô ích cho một giao dịch đã ghi nhận xong.
    var controller = CreateController(ServiceReturning(
        PaymentCallbackOutcome.PaidButCancelled, "Đơn hàng đã bị hủy nhưng giao dịch đã thu tiền.", 9));

    var result = await controller.Ipn();

    Assert.Equal("00", RspCodeOf(result));
}
```

- [ ] **Step 2: Chạy xác nhận đỏ**

Run: `dotnet test --filter "FullyQualifiedName~HandleCallback_WhenOrderCancelled"`
Expected: FAIL — test 1 nhận `Confirmed`/`Processing` thay vì `PaidButCancelled`/`Cancelled` (đây là bug H1 tái hiện dưới dạng test).

- [ ] **Step 3: Thêm guard vào `HandleCallbackAsync`**

Chèn **sau** khối kiểm tra số tiền (dòng 141) và **trước** `var executionStrategy = …` (dòng 143), với cùng cấu trúc execution-strategy + transaction như nhánh thành công:

- Nếu `order.Status == OrderStatus.Cancelled`: mở transaction, đặt `transaction.Status = Succeeded`, `transaction.UpdatedAt = DateTime.UtcNow`, `transaction.RawResponse = BuildRawResponse(query)`, `order.PaymentStatus = PaymentStatus.RefundPending` — **không** chạm `order.Status`, **không** chạm tồn kho (đã hoàn lúc huỷ) — rồi `SaveChangesAsync` + `CommitAsync`.
- Trước khi commit: `_logger.LogError("CẢNH BÁO HOÀN TIỀN: đơn hàng #{OrderId} đã bị hủy nhưng nhận được {Amount} qua {Provider} (ref {TransactionRef}) — cần hoàn tiền thủ công.", …)`. Dùng `LogError` (không phải `LogWarning`) vì đây là tiền của khách đang nằm sai chỗ.
- Trả `new PaymentCallbackHandlingResult { Success = true, Outcome = PaymentCallbackOutcome.PaidButCancelled, Message = $"Đơn hàng #{order.Id} đã bị hủy nhưng giao dịch đã được thu tiền. Bộ phận vận hành sẽ xử lý hoàn tiền.", OrderId = order.Id }`.
- Giữ nguyên nhánh thành công hiện có (dòng 144-170) cho các trạng thái khác.

Lưu ý double-đảm bảo idempotent: lần gọi thứ hai rơi vào guard `transaction.Status == Succeeded` (dòng 114) và trả `AlreadyProcessed` — **không** được di chuyển guard đó xuống dưới.

- [ ] **Step 4: Xử lý `PaidButCancelled` ở `Return`**

`PaymentController.Return`: `PaidButCancelled` không được rơi vào nhánh toast xanh. Điều kiện nhánh thành công giữ như Task 1 Step 1.5 (`Success && OrderId.HasValue && Outcome != PaidButCancelled`), và thêm nhánh riêng trước nhánh lỗi:

- `Outcome == PaidButCancelled` → `_notyf.Information(result.Message)` + `RedirectToAction(nameof(OrdersController.Details), "Orders", new { id = result.OrderId!.Value })`.
- Dùng `Information` (đã dùng ở `CartController.cs:106`), **không** dùng `Warning` — API chưa được chứng minh có trong `INotyfService` của repo này. Trạng thái `RefundPending` trên trang Details mới là chỗ nói rõ sự thật.
- IPN đã đúng sẵn qua ánh xạ `Outcome → "00"` của Task 1; chỉ cần test Step 1 xác nhận.

- [ ] **Step 5: Chạy test xác nhận xanh**

Run: `dotnet test --filter "FullyQualifiedName~HandleCallback_WhenOrderCancelled|FullyQualifiedName~Ipn_WhenPaidButCancelled"`
Expected: PASS.

- [ ] **Step 6: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS toàn bộ (114 + 3 test mới).

- [ ] **Step 7: Mutation check**

Bỏ dòng gán `order.PaymentStatus = PaymentStatus.RefundPending` trong nhánh mới, chạy lại 2 test Step 1, xác nhận đỏ, rồi restore.

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Services/Payments/PaymentService.cs ECommerce.Web/Controllers/PaymentController.cs ECommerce.Web.Tests/Services/PaymentServiceTests.cs ECommerce.Web.Tests/Controllers/PaymentControllerTests.cs
git commit -m "fix: refuse to resurrect a cancelled order when its payment callback arrives"
```

---

## Task 4: `PaymentMethod` — hiển thị lỗi + `id` radio duy nhất (L3, L2)

**Files:**
- Modify: `ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs:28-29`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml:66-84`
- Test: `ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs`

**Interfaces:**
- Consumes: `CheckoutViewModel` (đã có), `ICartService.GetCartAsync()`, `IOrderService.CreateOrderFromCartAsync` (đã có).
- Produces: `CheckoutViewModel.PaymentMethod` mang `[EnumDataType(typeof(PaymentMethod), …)]`; view render `<span asp-validation-for="PaymentMethod">`; mỗi radio một `id` duy nhất.

- [ ] **Step 1: Mở rộng helper `CreateController`**

Trong `CheckoutControllerTests.cs`, thêm tham số thứ ba `Mock<IPaymentService>? paymentService = null` (mặc định `new Mock<IPaymentService>()`) và truyền nó vào chỗ `new Mock<IPaymentService>().Object` ở dòng 41. Không thêm overload trùng; hai test hiện có vẫn gọi dạng 2 tham số.

- [ ] **Step 2: Viết hai test ghim hợp đồng**

Lưu ý trung thực: **cả hai test dưới đây PASS ngay khi viết** — controller hiện đã forward `model.PaymentMethod` (dòng 97) và đã re-render khi `ModelState` invalid (dòng 82-87). Chúng là test **ghim hồi quy** chứ không phải TDD đỏ-trước, và vì vậy Step 5 bắt buộc phải có mutation check để chứng minh chúng thực sự gánh tải. RED thật của task này là **HTML**, kiểm ở Step 6.

Trong `CheckoutControllerTests.cs` (dùng `CreateController` vừa mở rộng):

```csharp
[Fact]
public async Task Checkout_WhenPaymentMethodUndefined_ReRendersFormAndDoesNotCreateOrder()
{
    var orderService = new Mock<IOrderService>();
    var cartService = new Mock<ICartService>();
    cartService.Setup(s => s.GetCartAsync()).ReturnsAsync(new CartViewModel());

    var controller = CreateController(orderService, cartService);
    var model = new CheckoutViewModel { Cart = new CartViewModel() };
    // Model binding từ chối giá trị enum ngoài miền; test này pin rằng hệ quả là
    // render lại form chứ KHÔNG tạo đơn.
    controller.ModelState.AddModelError(nameof(CheckoutViewModel.PaymentMethod), "Vui lòng chọn phương thức thanh toán hợp lệ.");

    var result = await controller.Checkout(model);

    Assert.Equal("~/Views/Cart/CheckoutPreview.cshtml", Assert.IsType<ViewResult>(result).ViewName);
    orderService.Verify(
        s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>()),
        Times.Never);
}

[Fact]
public async Task Checkout_WhenValid_ForwardsSelectedPaymentMethodToOrderService()
{
    // Pin hợp đồng của R1: phương thức khách chọn phải được chuyển nguyên vẹn xuống service.
    var orderService = new Mock<IOrderService>();
    orderService
        .Setup(s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>()))
        .ReturnsAsync((true, "Đặt hàng thành công!", 77));
    var paymentService = new Mock<IPaymentService>();
    paymentService
        .Setup(s => s.StartPaymentAsync(It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
        .ReturnsAsync(new PaymentInitResult { Success = true });

    var cartService = new Mock<ICartService>();
    var controller = CreateController(orderService, cartService, paymentService);

    var model = new CheckoutViewModel
    {
        RecipientName = "Nguyen Van A",
        RecipientPhone = "0901234567",
        ShippingAddress = "12 Le Loi, Q1",
        PaymentMethod = PaymentMethod.VnPay,
        Cart = new CartViewModel()
    };

    var result = await controller.Checkout(model);

    orderService.Verify(
        s => s.CreateOrderFromCartAsync("user-checkout", It.IsAny<ShippingDetails>(), PaymentMethod.VnPay),
        Times.Once);
    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
    Assert.Equal(77, redirect.RouteValues?["id"]);
}
```

Cần mở rộng `CreateController` để nhận `Mock<IPaymentService>` (mặc định `new Mock<IPaymentService>()` khi không truyền) — sửa chữ ký helper, không thêm overload trùng.

- [ ] **Step 3: Chạy test, xác nhận cả hai PASS**

Run: `dotnet test --filter "FullyQualifiedName~Checkout_When"`
Expected: PASS cả hai. Nếu một test đỏ ở đây thì đó là bug thật mới phát hiện — dừng và báo, đừng sửa test cho khớp code.

- [ ] **Step 4: Thêm validation attribute**

`CheckoutViewModel.cs:28-29`:

```csharp
[EnumDataType(typeof(PaymentMethod), ErrorMessage = "Vui lòng chọn phương thức thanh toán hợp lệ.")]
[Display(Name = "Phương thức thanh toán")]
public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cod;
```

- [ ] **Step 4: Sửa view — fieldset, `id` duy nhất, span lỗi**

Trong `Views/Cart/CheckoutPreview.cshtml` khối dòng 66-84:

1. Thay `<label asp-for="PaymentMethod" …>` (dòng 67) bằng `<fieldset>` + `<legend class="block font-medium text-gray-700 dark:text-gray-300 mb-1">Phương thức thanh toán</legend>`; đóng `</fieldset>` sau `#vnPayNote` (dòng 83). Lý do: `<label asp-for>` trỏ `for="PaymentMethod"` vào radio **đầu tiên**, làm nhãn nhóm bị đọc lệch.
2. Bỏ `asp-for` trên hai `<input type="radio">` (dòng 70, 75) — `asp-for` sinh `id="PaymentMethod"` cho **cả hai** (đo được `id_count=2`) — và thay bằng markup tường minh, giữ đúng `name` để binding vẫn hoạt động:
   - COD: `name="PaymentMethod" value="Cod" id="paymentMethodCod"` + `@(Model.PaymentMethod == PaymentMethod.Cod ? "checked" : null)`
   - VNPay: `name="PaymentMethod" value="VnPay" id="paymentMethodVnPay"` + `@(Model.PaymentMethod == PaymentMethod.VnPay ? "checked" : null)`
   Giữ nguyên toàn bộ `class` hiện có trên input để không phá style.
3. Thêm ngay sau `</div>` của `#paymentOptions` (trước `#vnPayNote`):
   `<span asp-validation-for="PaymentMethod" class="block mt-1 text-xs text-red-600"></span>`
4. **Không** đổi selector trong `<script>` dòng 157/170: chúng dùng `input[name="PaymentMethod"]` và `input[value="VnPay"]` — vẫn khớp sau khi bỏ `asp-for`.

- [ ] **Step 5: Chạy test + mutation check**

Run: `dotnet build --no-incremental` rồi `dotnet test`
Expected: PASS toàn bộ.

Mutation: đổi `PaymentMethod = PaymentMethod.VnPay` trong test 2 thành `PaymentMethod.Cod` → test phải đỏ (`Verify` với `VnPay`). Restore.

- [ ] **Step 6: Kiểm tra thủ công bằng trình duyệt (bắt buộc — đây là thay đổi view)**

Khởi động dev server **trong một command duy nhất** (boot + drive + kill, như A10; nhớ `$pid` là biến read-only, dùng tên khác) và xác nhận:
1. Trang `/Cart/CheckoutPreview` không còn `id="PaymentMethod"` trùng — đếm `id="paymentMethodCod"` = 1, `id="paymentMethodVnPay"` = 1.
2. Chọn VNPay rồi submit → 302 sang cổng thanh toán (regression: `checked` tường minh vẫn gửi đúng VNPay).
3. POST `PaymentMethod=999` → 200 re-render và **có** dòng chữ "Vui lòng chọn phương thức thanh toán hợp lệ." trong HTML.
Sau đó tắt server, xác nhận 0 listener ở cổng đã dùng.

- [ ] **Step 7: Commit**

```bash
git add ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs ECommerce.Web/Views/Cart/CheckoutPreview.cshtml ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs
git commit -m "fix: surface payment-method validation error and give each radio a unique id"
```

---

## Task 5: View admin dùng `ProductImage.Placeholder` (L4)

**Files:**
- Modify: `ECommerce.Web/Views/Admin/Products.cshtml:96`
- Modify: `ECommerce.Web/Views/Admin/ProductForm.cshtml:78`, `:111`

**Interfaces:**
- Consumes: `ProductImage.Resolve(string?)` và `ProductImage.Placeholder` (namespace `ECommerce.Web.Models`, đã có trong `Views/_ViewImports.cshtml:2`).
- Produces: không.

- [ ] **Step 1: Thay ba chỗ hardcode**

- `Views/Admin/Products.cshtml:96` — `src` → `@ProductImage.Resolve(product.ImageUrl)`; trong `onerror` thay literal bằng `this.src='@ProductImage.Placeholder'`.
- `Views/Admin/ProductForm.cshtml:78` — `src` → `@ProductImage.Resolve(Model.ImageUrl)`; `onerror` → `this.src='@ProductImage.Placeholder'`.
- `Views/Admin/ProductForm.cshtml:111` — `var fallback = '/img/product-placeholder.svg';` → `var fallback = '@ProductImage.Placeholder';`.

Giữ nguyên `onerror="this.onerror=null;…"` (chống loop) và toàn bộ class.

- [ ] **Step 2: Xác nhận không còn literal trong view**

Run: `Select-String -Path "ECommerce.Web/Views/**/*.cshtml" -Pattern "product-placeholder.svg"`
Expected: **không** có kết quả. (Còn 1 kết quả duy nhất ở `ECommerce.Web/Models/ProductImage.cs:7` là đúng — đó là nguồn.)

- [ ] **Step 3: Build + test**

Run: `dotnet build --no-incremental` rồi `dotnet test`
Expected: PASS. (Razor lỗi cú pháp chỉ lộ khi build — bước này là kiểm chứng thật của task.)

- [ ] **Step 4: Commit**

```bash
git add ECommerce.Web/Views/Admin/Products.cshtml ECommerce.Web/Views/Admin/ProductForm.cshtml
git commit -m "refactor: use ProductImage.Placeholder in admin views"
```

---

## Task 6: Golden vector cho canonical string + chữ ký VNPay (L5)

**Files:**
- Test: `ECommerce.Web.Tests/Services/VnPayGatewayTests.cs`
- Test: `ECommerce.Web.Tests/Helpers/VnPaySignatureHelper.cs` (chỉ thêm method dựng vector, không sửa thuật toán)

**Interfaces:**
- Consumes: `VnPaySignatureHelper.Canonical`/`Sign`, `VnPayGateway.CreatePaymentAsync`.
- Produces: không (test-only).

- [ ] **Step 1: Viết test ghim canonical string**

Thêm vào `VnPayGatewayTests.cs`. Bộ input cố định được chọn để chạm đúng các quy tắc dễ sai: khoá cần **sort** (`vnp_TxnRef` trước `vnp_TmnCode` theo ordinal? — pin chính xác chuỗi, đừng suy luận), một value chứa **khoảng trắng** (encode `+` hay `%20`), và một value **rỗng** (phải bị loại khỏi chuỗi ký):

```csharp
[Fact]
public void Canonical_PinsSortingEncodingAndEmptyValueRule()
{
    var parameters = new Dictionary<string, string>
    {
        ["vnp_TxnRef"] = "5-1700000000000",
        ["vnp_OrderInfo"] = "Thanh toan don hang 5",
        ["vnp_Empty"] = string.Empty,
        ["vnp_TmnCode"] = "T"
    };

    var canonical = VnPaySignatureHelper.Canonical(parameters);

    Assert.Equal("<CHUỖI GHIM ĐƯỢC IN RA Ở BƯỚC 2>", canonical);
}
```

- [ ] **Step 2: In chuỗi thật rồi ghim**

Run test ở Step 1 → FAIL, output hiện chuỗi thực tế. **Đọc** chuỗi đó và kiểm tra bằng mắt ba quy tắc (thứ tự sort, encode khoảng trắng, value rỗng bị bỏ). Chỉ khi ba quy tắc đúng theo spec VNPay mới chép chuỗi đó vào `Assert.Equal`. Nếu một quy tắc **sai**, đây là phát hiện thật → dừng và báo, không sửa test cho khớp code.

- [ ] **Step 3: Ghim hex của chữ ký**

Thêm `[Fact] Canonical_SignatureForFixedInput_MatchesPinnedHex`: ký đúng bộ tham số trên với `HashSecret = "secret"`, in `Sign(...)` ở lần chạy đầu, ghim hex nhận được, chạy lại xác nhận PASS.

Ghi rõ trong comment của test rằng hex này được tính **một lần** từ implementation hiện tại nên nó ghim **hồi quy**, không chứng minh đúng spec; và rằng giá trị TmnCode/HashSecret ở đây là giả, không phải credentials thật.

- [ ] **Step 4: Chạy test**

Run: `dotnet test --filter "FullyQualifiedName~VnPayGatewayTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ECommerce.Web.Tests/Services/VnPayGatewayTests.cs
git commit -m "test: pin canonical VNPay signing string and signature hex"
```

**Residual (ghi vào report, không sửa ở đây):** chỉ round-trip sandbox VNPay thật mới xác nhận end-to-end; vector chính thức do VNPay công bố chưa được đối chiếu.

---

## Task 7: `StartPaymentAsync` gọi lại không được 500 (L6)

**Files:**
- Modify: `ECommerce.Web/Services/Payments/PaymentService.cs:56-72`
- Modify: `ECommerce.Web/Controllers/OrdersController.cs:108-121`
- Test: `ECommerce.Web.Tests/Services/PaymentServiceTests.cs`
- Test: `ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs`

**Interfaces:**
- Consumes: `PaymentTransaction` (đã có), `PaymentInitResult` (đã có).
- Produces: `StartPaymentAsync` trả về kết quả **idempotent** (không exception, không chèn thêm) khi đã tồn tại transaction cùng `(Provider, TransactionRef)`.

- [ ] **Step 1: Viết test thất bại (service)**

```csharp
[Fact]
public async Task StartPaymentAsync_WhenSameTransactionRefExists_ReusesInsteadOfInserting()
{
    // COD dùng ref tất định "COD-{orderId}". Gọi lần hai (double-click, hoặc nút
    // "thanh toán lại" ở Phase B) không được nổ unique index trên (Provider, TransactionRef).
    var order = await SeedOrderAsync();

    var first = await _service.StartPaymentAsync(order.Id, PaymentMethod.Cod, "127.0.0.1");
    var second = await _service.StartPaymentAsync(order.Id, PaymentMethod.Cod, "127.0.0.1");

    Assert.True(first.Success);
    Assert.True(second.Success);
    Assert.Equal(first.TransactionRef, second.TransactionRef);
    Assert.Equal(1, await _db.PaymentTransactions.CountAsync());
}
```

- [ ] **Step 2: Chạy xác nhận đỏ**

Run: `dotnet test --filter "FullyQualifiedName~StartPaymentAsync_WhenSameTransactionRefExists"`
Expected: FAIL bằng `DbUpdateException` (hoặc `SqliteException` unique constraint) từ lần gọi thứ hai.

- [ ] **Step 3: Làm `StartPaymentAsync` idempotent theo ref**

Trong `PaymentService.cs`, sau `if (!result.Success) return result;` (dòng 59) và **trước** `_db.PaymentTransactions.Add(...)` (dòng 62): nếu `result.TransactionRef` không rỗng và đã tồn tại transaction với `t.Provider == gateway.Provider && t.TransactionRef == result.TransactionRef`, thì `LogInformation` rằng đang tái dùng lượt thanh toán đã có và `return result;` (không chèn mới).

Đây cố ý **chỉ** dedup theo ref: với VNPay mỗi lượt sinh ref mới nên vẫn tạo transaction mới + URL mới (đúng ngữ nghĩa), còn COD tái dùng.

- [ ] **Step 4: Viết test thất bại (controller) rồi bọc try/catch**

```csharp
[Fact]
public async Task Checkout_WhenStartPaymentThrows_StillRedirectsToOrderDetails()
{
    // Đơn đã tạo (tồn kho đã trừ). Lỗi ở bước khởi tạo thanh toán KHÔNG được biến
    // thành 500 — đơn vẫn phải nằm trong lịch sử ở trạng thái chưa thanh toán.
    var orderService = new Mock<IOrderService>();
    orderService
        .Setup(s => s.CreateOrderFromCartAsync(It.IsAny<string>(), It.IsAny<ShippingDetails>(), It.IsAny<PaymentMethod>()))
        .ReturnsAsync((true, "Đặt hàng thành công!", 88));
    var paymentService = new Mock<IPaymentService>();
    paymentService
        .Setup(s => s.StartPaymentAsync(It.IsAny<int>(), It.IsAny<PaymentMethod>(), It.IsAny<string>()))
        .ThrowsAsync(new InvalidOperationException("boom"));

    var controller = CreateController(orderService, new Mock<ICartService>(), paymentService);
    var model = new CheckoutViewModel
    {
        RecipientName = "A", RecipientPhone = "0901234567", ShippingAddress = "12 Le Loi",
        PaymentMethod = PaymentMethod.Cod, Cart = new CartViewModel()
    };

    var result = await controller.Checkout(model);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal(nameof(OrdersController.Details), redirect.ActionName);
    Assert.Equal(88, redirect.RouteValues?["id"]);
}
```

`OrdersController.Checkout` dòng 108-121: bọc lời gọi `StartPaymentAsync` trong `try/catch (Exception ex)`; trong `catch` gọi `_logger.LogError(ex, "…")` rồi đi tiếp xuống luồng thành công hiện có (`_notyf.Success(message); return RedirectToAction(nameof(Details), …)`). Giữ nguyên comment dòng 104-107 — nó mô tả đúng ý định này.

- [ ] **Step 5: Chạy test xác nhận xanh**

Run: `dotnet test --filter "FullyQualifiedName~StartPaymentAsync_WhenSameTransactionRefExists|FullyQualifiedName~Checkout_WhenStartPaymentThrows"`
Expected: PASS.

- [ ] **Step 6: Mutation check**

Bỏ khối dedup ở Step 3 → test service phải đỏ trở lại. Restore.

- [ ] **Step 7: Chạy toàn bộ test + build**

Run: `dotnet build --no-incremental` rồi `dotnet test`
Expected: 0 warning, PASS toàn bộ.

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Services/Payments/PaymentService.cs ECommerce.Web/Controllers/OrdersController.cs ECommerce.Web.Tests/Services/PaymentServiceTests.cs ECommerce.Web.Tests/Controllers/CheckoutControllerTests.cs
git commit -m "fix: make payment initiation idempotent per transaction ref and never 500 checkout"
```

---

## Sau khi xong tất cả task

- [ ] Chạy `gitnexus detect-changes --scope all` (hoặc MCP `detect_changes`) trước khi kết luận; `partial: true`/`truncated: true` **không** phải là "sạch".
- [ ] Cập nhật ledger `.superpowers/sdd/2026-10-18-ecommerce-v2-core-commerce/progress.md` và viết report cho đợt fix này (kể cả các residual: chưa round-trip sandbox VNPay; không có quy trình hoàn tiền tự động).
- [ ] Chạy lại live-verify cho đúng 3 kịch bản review: (a) huỷ đơn VNPay trước IPN → sau IPN đơn vẫn `Cancelled` + badge "Chờ hoàn tiền"; (b) huỷ đơn đã thanh toán → "Chờ hoàn tiền" (không phải "Đã hoàn tiền") ở storefront và admin; (c) admin chuyển `RefundPending` → `Refunded` qua form có sẵn.
