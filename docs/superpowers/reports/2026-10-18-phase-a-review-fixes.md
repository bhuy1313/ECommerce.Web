# Report — Đợt sửa lỗi self-review Phase A

- **Plan:** `docs/superpowers/plans/2026-10-18-phase-a-review-fixes.md`
- **Ledger:** `.superpowers/sdd/2026-10-18-phase-a-review-fixes/progress.md`
- **Base:** `8eb0ef3` (hết Phase A) → **HEAD:`7e149a6`**
- **Chế độ:** inline (orchestrator tự chạy từng task, TDD đỏ-trước), không worktree.
- **Test:** 114 (baseline) → **130** pass, **0 warning** khi `dotnet build --no-incremental`.

## 1. Kết quả theo task

| # | Lỗi | Sửa gì | Bằng chứng đỏ (trước khi sửa) | Commit |
|---|-----|--------|-------------------------------|--------|
| 1 | L1 — phân loại callback bằng cách dò câu chữ thông báo | `PaymentCallbackHandlingResult` + enum `PaymentCallbackOutcome`; `ResolveRspCode(outcome)` switch | 6 lỗi compile chỉ ở file test | `f6e991d` |
| 2 | M1 — đơn đã huỷ + đã thu tiền bị ghi "Đã hoàn tiền" (nói dối) | `PaymentStatus.RefundPending = 4`; storefront + admin render "Chờ hoàn tiền" | `CS0117: 'PaymentStatus' does not contain 'RefundPending'` | `4f6c903` |
| 3 | H1 — IPN đến sau khi huỷ đơn thì **hồi sinh** đơn đã huỷ thành Paid | Guard `order.Status == OrderStatus.Cancelled`: giữ `Cancelled`, đặt `RefundPending`, transaction `Succeeded`, `LogError` cảnh báo hoàn tiền, trả `PaidButCancelled` → RspCode `00` + toast Information | `Expected: PaidButCancelled / Actual: Confirmed` | `24dce86` |
| 4 | L2 id radio trùng; L3 lỗi validate không hiển thị | `fieldset/legend` + id duy nhất cho 2 radio + `asp-validation-for`; Việt hoá message của model binder | — (pinning tests pass ngay, **mutation check** thay thế) | `c1a37ea` |
| 5 | L4 — 3 chỗ hardcode `/img/product-placeholder.svg` | Dùng `ProductImage.Resolve` / `.Placeholder` | 0 literal còn lại trong view (nguồn duy nhất: `ProductImage.cs:7`) | `333dc05` |
| 6 | L5 — test tự ký bằng bản sao thuật toán nên không ghim gì | Vector vàng: ghim canonical string + hex chữ ký | placeholder `PIN_CANONICAL`/`PIN_HEX` → in ra giá trị thật | `3bd9dcb` |
| 7 | L6 — `StartPaymentAsync` gọi lần 2 nổ unique index (500 ở checkout) | Dedup theo `(Provider, TransactionRef)`; `OrdersController.Checkout` bọc try/catch | `SqliteException 19 UNIQUE constraint failed: PaymentTransactions.Provider, TransactionRef`; và `InvalidOperationException: boom` lọt ra khỏi action | `7e149a6` |

Test theo task: 114 → 121 → 121 → 124 → 126 → 126 → 128 → **130**.

## 2. Phát hiện quan trọng: plan sai ở Task 4

Plan dự đoán message lỗi hiển thị là `ErrorMessage` của `[EnumDataType]` ("Vui lòng chọn phương thức thanh toán hợp lệ."). **Sai.** Với `PaymentMethod=999`, model binder từ chối enum ngoài miền **trước khi** validation chạy, nên message là mặc định của framework — tiếng Anh: `The value '999' is invalid.` trên một storefront tiếng Việt.

- Sửa: `builder.Services.Configure<MvcOptions>` trong `Program.cs` đặt `AttemptedValueIsInvalidAccessor` + `ValueIsInvalidAccessor` sang tiếng Việt. Nhánh thực tế chạy là `ValueIsInvalid` → render `Giá trị '999' không hợp lệ.` (không kèm tên trường).
- `Program.cs` **không nằm trong** danh sách file của plan cho task này → deviation đã ghi ledger.
- Giữ `[EnumDataType]` làm backstop (không còn đường nào chạy tới, nhưng vô hại).

## 3. Live verify — 3 kịch bản review (24/24 check xanh)

Chạy 1 command duy nhất (boot → drive HTTP → kill), QA `qa.a4@technova.local`, admin `admin@ecommerce.local`, product `12008`.

Kịch bản A — huỷ đơn VNPay **trước** IPN (đơn `16013`):
`A_cancel_redirected=True` · badge trước IPN = "Chưa thanh toán" · sau IPN: `A_ipn_rspcode_00=True`, `A_still_cancelled_after_ipn=True`, `A_badge_cho_hoan_tien=True`, `A_not_marked_refunded=True`, `A_refund_banner_sentence=True`, `A_cancel_button_gone=True`.
Message IPN trả về: *"Đơn hàng #16013 đã bị hủy nhưng giao dịch đã được thu tiền. Bộ phận vận hành sẽ xử lý hoàn tiền."*

Kịch bản B — thanh toán xong rồi huỷ (đơn `16014`):
`B_ipn_rspcode_00=True` → `B_paid_badge=True` → huỷ → `B_storefront_cho_hoan_tien=True`, `B_storefront_not_da_hoan_tien=True`, `B_storefront_refund_banner=True`, `B_admin_detail_refundpending=True`, `B_admin_list_cho_hoan_tien=True`.

Kịch bản C — admin chuyển `RefundPending` → `Refunded` bằng form có sẵn:
`C_update_redirected=True`, `C_admin_detail_refunded=True`, `C_storefront_da_hoan_tien=True`, `C_storefront_no_longer_pending=True`.

Cleanup: `listeners_on_5080=0 app_processes=0`.

**Giá trị phụ:** chữ ký IPN trong kịch bản A/B được tính bằng một **bản cài đặt PowerShell độc lập** của quy tắc canonical (sort ordinal + urlencode + bỏ value rỗng), và app thật **chấp nhận** chữ ký đó. Đây là round-trip thật của quy tắc ký qua đúng code path production.

## 4. Graph change analysis

- `detect-changes --scope all`: `changed_count=6`, chỉ `docs/roadmap.md` (không thuộc đợt này), `affected_count=0`, `risk=low`; **không** có `partial`/`truncated`.
- `detect-changes --scope compare --base-ref 8eb0ef3`: `risk=critical`, 25 process bị ảnh hưởng — **đúng như dự kiến**: mọi symbol đổi đều nằm trên luồng payment/checkout (`Ipn`, `Return`, `Checkout`, `StartPaymentAsync`, `CancelOrderAsync`, `HandleCallbackAsync`), đây chính là các luồng đợt sửa nhắm vào. Không xuất hiện consumer ngoài dự kiến; toàn bộ bề mặt được phủ bởi 130 test + live verify ở mục 3.
- Index đã refresh lại (cuối cùng: `--pdg`, 4,199 nodes / 7,576 edges / 42 clusters / 50 flows).

## 5. Residual (chưa giải quyết, nằm ngoài scope đợt này)

1. **Chưa round-trip sandbox VNPay.** Vector vàng ghim *hồi quy*, không chứng minh đúng spec; chưa đối chiếu vector chính thức của VNPay. Giảm nhẹ: quy tắc ký đã round-trip qua app thật (mục 3).
2. **Chưa có quy trình hoàn tiền tự động.** `RefundPending` là trạng thái công việc thủ công; admin phải tự chuyển sang `Refunded` (quyết định D2). Storefront chỉ nói "đang chờ được hoàn lại".
3. **`ErrorMessage` của `[EnumDataType]` là dead code** với giá trị enum ngoài miền (binding chặn trước). Giữ làm backstop.
4. **Dữ liệu dev bị tạo thêm:** đơn `16013`, `16014` (verify này) + `12012`, `12013` + vài đơn rác từ probe Task 4 — chỉ tồn tại ở LocalDB dev.
5. **Phase B và Phase C** của plan gốc `2026-10-18-ecommerce-v2-core-commerce.md` vẫn chưa làm (PHASE B@636, PHASE C@662).
6. **File chưa commit (cố ý):** `docs/roadmap.md` (M) và `opencode.json` (M) là thay đổi có trước, không thuộc đợt sửa nên không stage. Hai file plan trong `docs/superpowers/plans/` đang untracked — chờ quyết định của bạn có commit hay không. (`git status` còn ~95 file " M" là nhiễu EOL `core.autocrlf`, đã biết.)
