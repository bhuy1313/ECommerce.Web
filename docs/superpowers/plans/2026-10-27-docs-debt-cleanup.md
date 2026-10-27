# Dọn nợ tài liệu (docs debt cleanup) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Đưa bộ tài liệu trong `docs/` về trạng thái khớp 100% với code đã ship: một nguồn chân lý cho tiến độ, plan có trạng thái rõ ràng, báo cáo kiểm chứng được lưu trong git, và một quy trình đóng phase để nợ không tái phát.

**Architecture:** Không sửa code. Sáu thay đổi độc lập, làm theo thứ tự phụ thuộc: (1) archive báo cáo vào `docs/superpowers/reports/` để link có đích; (2) thêm banner trạng thái + tick hộp kiểm hồi tố cho 8 plan đã xong; (3) viết lại phần trạng thái của `docs/roadmap.md` kèm bảng nhật ký thực thi; (4) thêm `docs/README.md` làm bản đồ tài liệu + checklist đóng phase; (5) xoá `specs/` rác ở root; (6) quét kiểm chứng toàn bộ + ghi report.

**Tech Stack:** Markdown (UTF-8, LF), git (pathspec tường minh), PowerShell 7 (đọc/ghi UTF-8), GitNexus CLI (`detect-changes`), `dotnet test`.

**Spec:** Không có spec riêng — người dùng yêu cầu "planning kĩ" cho việc dọn nợ tài liệu và đã bỏ qua vòng hỏi đáp. Nguồn sự thật của plan này là §Hiện trạng (đã kiểm bằng lệnh, ghi ngay bên dưới) + `docs/roadmap.md` hiện tại. Mọi quyết định không có trong yêu cầu được đánh dấu **Quyết định của tôi** để người đọc kiểm lại.

## Hiện trạng (đã kiểm ngày 2026-10-27)

| Hạng mục | Giá trị thực tế | Lệnh đã dùng |
|---|---|---|
| HEAD | `46edad9` trên `main` (2026-10-22) | `git log -1 --format='%h %ad %s' --date=short` |
| Test suite | **266/266 PASS**, 0 warning | `dotnet test --nologo -v q` |
| Plan files | 9 (2 file v1 + 7 file v2), tất cả **untracked** trừ 2 file v1 | `git status --short` |
| Hộp kiểm chưa tick (đầu dòng) | 292 hộp trong 8 plan đã hoàn thành | `Select-String '^\s*-\s*\[ \]'` |
| `[ ]` nằm giữa dòng | 7 chỗ — đều là câu mẫu `` `- [ ]` `` trong dòng "For agentic workers", **không được thay** | `Select-String '\[ \]'` lọc `-notmatch '^\s*-\s*\[ \]'` |
| Báo cáo SDD | 6 file report (A, B1–B5) trong `.superpowers/sdd/`, **bị gitignore cục bộ** (`.superpowers/sdd/.gitignore` chứa `*`) | `git check-ignore -v …` |
| Roadmap Phase 10 | 15 hộp trống = 5 P0 + 5 P1 + 5 P2; P0/P1 **đã xong trong code** | đọc `docs/roadmap.md:106-125` |
| `specs/` ở root | Chỉ có `specs/README.md` ("directory for test plans"), **đã track từ `9256977`**; Playwright dùng `testDir: './tests'` → **rác** | `Get-Content specs/README.md`, `playwright.config.ts:4`, `git ls-files -s specs` |
| File có diff nội dung thật | Chỉ `docs/roadmap.md` và `opencode.json`; phần còn lại là nhiễu LF/CRLF | `git diff --stat -- docs ECommerce.Web …` |
| Commit range các đợt | v1 `9256977..a1251cb`; deploy `abb7afa..44a11f6`; A `229a62e..8eb0ef3`; fix A `f6e991d..7e149a6`; B1 `9011743..ac5b1ea`; B2 `e04ac31..c4afdc6`; B3 `910b49d..8f89825`; B4 `865ce74..1f35832`; B5 `bfaa5f7..46edad9` | `git log --reverse` |

## Global Constraints

- **Chỉ đụng vào `docs/**` và xoá `specs/`.** Không sửa `*.cs`, `*.cshtml`, `*.csproj`, `*.slnx`, `appsettings*`, `Program.cs`, migrations, test, `.gitignore`, pipeline.
- **Không sửa `opencode.json`** — đó là thay đổi cục bộ của người dùng (model agent), ngoài phạm vi.
- **Không viết lại lịch sử.** Nội dung plan/report chỉ được thêm banner trạng thái và đổi `- [ ]` → `- [x]`; không sửa câu chữ, số liệu, hay thứ tự bước.
- **Chỉ tick khi có bằng chứng.** Một bước chỉ được tick khi commit range hoặc report tương ứng chứng minh đã làm. Không chắc → để `[ ]` và ghi lý do trong banner.
- **Không tick dòng mẫu.** Chỉ thay các dòng khớp `^\s*- \[ \]`. Dòng 3 của mỗi plan chứa `` (`- [ ]`) `` là ví dụ cú pháp, phải giữ nguyên `[ ]`.
- **Encoding:** mọi file ghi ra phải **UTF-8 không BOM, xuống dòng LF**. Không dùng `Set-Content`/`Out-File` mặc định (PowerShell 5.1 ghi UTF-16/ANSI). Dùng `edit`/`write` tool hoặc `[IO.File]::WriteAllText($p,$s,[Text.UTF8Encoding]::new($false))`.
- **Commit chỉ chứa tài liệu** (`docs/**`; riêng Task 5 xoá `specs/README.md`), thêm bằng pathspec tường minh (`git add docs/...`), không `git add -A` — working tree có sẵn nhiễu CRLF ở `.agents/`, `wwwroot/lib/`, `site.css`, `site.js`, `.slnx`…
- **Mỗi commit chạy graph gate** theo `AGENTS.md`: `node .gitnexus/run.cjs detect-changes --scope all`; kỳ vọng chỉ thấy section `docs/**`, risk `low`; thấy bất kỳ file code nào → **dừng, báo người dùng**. Exit code `1` là bình thường khi có thay đổi; `partial`/`truncated` thì chạy lại.
- **Ngôn ngữ:** tiếng Việt, giữ thuật ngữ kỹ thuật tiếng Anh (commit, test, plan, report…). Không dịch lại tài liệu cũ.
- **Không thêm `.gitattributes`/`renormalize`** trong plan này (xem §Ngoài phạm vi).
- **Không tự bịa số liệu.** Số test sau mỗi đợt lấy đúng từ report tương ứng; nếu report không có thì ghi `—`.

## Review Focus

1. **Link tương đối hỏng** — mọi đường dẫn kiểu `../reports/<file>.md` và `superpowers/<...>` phải resolve; Task 4 quét toàn bộ `docs/**/*.md` (bỏ qua code fence).
2. **Tick sai sự thật** — một bước chưa làm mà bị tick sẽ biến tài liệu thành lời nói dối; chỉ tick theo commit/report, sai lệch thì dừng.
3. **Mojibake** — tiếng Việt bị `�` do ghi sai encoding; kiểm bằng cách đọc lại file sau khi ghi.
4. **Khẳng định production sai** — nếu `/healthz` không trả 200, roadmap phải ghi "kiểm tra lần cuối 2026-10-22", không được khẳng định "đang chạy".
5. **Nợ tái phát** — không có checklist đóng phase trong `docs/README.md` thì 3 tháng sau lại phải dọn; Task 4 là bắt buộc, không phải "nice to have".

---

## File Structure

**Tạo mới**

| File | Trách nhiệm |
|---|---|
| `docs/README.md` | Bản đồ tài liệu, trạng thái snapshot, quy ước, checklist đóng phase |
| `docs/superpowers/reports/2026-10-14-verify-legacy-phases.md` | Report v1 (di chuyển từ `docs/verify-legacy-phases-report.md`, giữ nguyên nội dung) |
| `docs/superpowers/reports/2026-10-18-ecommerce-v2-core-commerce.md` | Report **tổng hợp** đợt P0/A1–A10 (viết mới từ ledger + commit) |
| `docs/superpowers/reports/2026-10-18-phase-a-review-fixes.md` | Bản sao verbatim report A |
| `docs/superpowers/reports/2026-10-19-phase-b1-email-flows.md` | Bản sao verbatim report B1 |
| `docs/superpowers/reports/2026-10-19-phase-b2-profile-address-book.md` | Bản sao verbatim report B2 |
| `docs/superpowers/reports/2026-10-19-phase-b3-coupons.md` | Bản sao verbatim report B3 |
| `docs/superpowers/reports/2026-10-22-phase-b4-reviews.md` | Bản sao verbatim report B4 |
| `docs/superpowers/reports/2026-10-22-phase-b5-shipping-tax.md` | Bản sao verbatim report B5 |
| `docs/superpowers/reports/2026-10-27-docs-debt-cleanup.md` | Report kiểm chứng của chính plan này (Task 6) |

**Sửa**

| File | Thay đổi |
|---|---|
| `docs/roadmap.md` | Khối trạng thái đầu file, tick 10 hộp P0/P1, bảng nhật ký thực thi, sửa 3 chỗ sai/lỗi thời |
| `docs/superpowers/plans/2026-10-14-verify-legacy-phases.md` | Banner + tick 45 hộp |
| `docs/superpowers/plans/2026-10-16-render-free-sqlite-deploy.md` | Banner + tick 21 hộp |
| `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` | Banner + tick 68 hộp |
| `docs/superpowers/plans/2026-10-18-phase-a-review-fixes.md` | Banner + tick 51 hộp |
| `docs/superpowers/plans/2026-10-19-phase-b1-email-flows.md` | Banner + tick 38 hộp |
| `docs/superpowers/plans/2026-10-19-phase-b2-profile-address-book.md` | Banner + tick 28 hộp |
| `docs/superpowers/plans/2026-10-19-phase-b3-coupons.md` | Banner + tick 23 hộp |
| `docs/superpowers/plans/2026-10-22-phase-b4-reviews.md` | Banner + tick 18 hộp |
| `docs/superpowers/plans/2026-10-22-phase-b5-shipping-tax.md` | Chỉ banner (đã tick đủ 19) |

**Xoá**

| File | Lý do |
|---|---|
| `specs/README.md` + thư mục rỗng `specs/` | Placeholder không được dùng; Playwright dùng `./tests`. File **đã track từ `9256977`** nên xoá bằng `git rm` và có 1 commit. |

**Không đụng:** `.superpowers/sdd/**` (giữ làm working artifact, có `.gitignore` riêng), `agent/`, `.agents/`, `.claude/`, `e_commerce_app.html`, `qu_n_tr_e_commerce_app.html` (ghi nhận trong README là template gốc).

---

### Task 1: Archive báo cáo vào git + baseline hoá plan/spec đang untracked

**Files:**
- Create: `docs/superpowers/reports/` (1 bản tổng hợp mới + 6 bản sao report, xem bảng File Structure)
- Move: `docs/verify-legacy-phases-report.md` → `docs/superpowers/reports/2026-10-14-verify-legacy-phases.md`
- Commit baseline: 7 plan v2 + plan này + 5 spec đang untracked

**Interfaces:**
- Consumes: 6 report trong `.superpowers/sdd/*/report-*.md`; 12 file docs đang untracked.
- Produces: đường dẫn report ổn định để Task 2 (banner plan) và Task 3/4 (roadmap, index) link tới; baseline trong git để `git diff` ở Task 2 soát được thay đổi tick/banner.

- [ ] **Step 1: Tạo thư mục và copy 6 report (A, B1, B2, B3, B4, B5)**

```powershell
New-Item -ItemType Directory -Force docs/superpowers/reports | Out-Null
$map = @{
  '.superpowers/sdd/2026-10-18-phase-a-review-fixes/report-phase-a-review-fixes.md' = 'docs/superpowers/reports/2026-10-18-phase-a-review-fixes.md'
  '.superpowers/sdd/2026-10-19-phase-b1-email-flows/report-phase-b1-email-flows.md'   = 'docs/superpowers/reports/2026-10-19-phase-b1-email-flows.md'
  '.superpowers/sdd/2026-10-19-phase-b2-profile-address-book/report-phase-b2-profile-address-book.md' = 'docs/superpowers/reports/2026-10-19-phase-b2-profile-address-book.md'
  '.superpowers/sdd/2026-10-19-phase-b3-coupons/report-phase-b3-coupons.md'           = 'docs/superpowers/reports/2026-10-19-phase-b3-coupons.md'
  '.superpowers/sdd/2026-10-22-phase-b4-reviews/report-phase-b4-reviews.md'           = 'docs/superpowers/reports/2026-10-22-phase-b4-reviews.md'
  '.superpowers/sdd/2026-10-22-phase-b5-shipping-tax/report-phase-b5-shipping-tax.md' = 'docs/superpowers/reports/2026-10-22-phase-b5-shipping-tax.md'
}
foreach ($k in $map.Keys) {
  $text = [IO.File]::ReadAllText($k).TrimStart([char]0xFEFF)   # B3 có BOM; bỏ BOM cho đồng nhất
  [IO.File]::WriteAllText($map[$k], $text, [Text.UTF8Encoding]::new($false))
}
```

- [ ] **Step 2: Xác nhận bản sao khớp nguồn (sau khi bỏ BOM)**

```powershell
$map = @{
  '.superpowers/sdd/2026-10-18-phase-a-review-fixes/report-phase-a-review-fixes.md' = 'docs/superpowers/reports/2026-10-18-phase-a-review-fixes.md'
  '.superpowers/sdd/2026-10-19-phase-b1-email-flows/report-phase-b1-email-flows.md'   = 'docs/superpowers/reports/2026-10-19-phase-b1-email-flows.md'
  '.superpowers/sdd/2026-10-19-phase-b2-profile-address-book/report-phase-b2-profile-address-book.md' = 'docs/superpowers/reports/2026-10-19-phase-b2-profile-address-book.md'
  '.superpowers/sdd/2026-10-19-phase-b3-coupons/report-phase-b3-coupons.md'           = 'docs/superpowers/reports/2026-10-19-phase-b3-coupons.md'
  '.superpowers/sdd/2026-10-22-phase-b4-reviews/report-phase-b4-reviews.md'           = 'docs/superpowers/reports/2026-10-22-phase-b4-reviews.md'
  '.superpowers/sdd/2026-10-22-phase-b5-shipping-tax/report-phase-b5-shipping-tax.md' = 'docs/superpowers/reports/2026-10-22-phase-b5-shipping-tax.md'
}
foreach ($k in $map.Keys) {
  $src = [IO.File]::ReadAllText($k).TrimStart([char]0xFEFF).Replace("`r`n","`n")
  $dst = [IO.File]::ReadAllText($map[$k]).Replace("`r`n","`n")
  "{0} {1}" -f ($(if ($src -ceq $dst) {'OK  '} else {'DIFF'})), $map[$k]
}
```

Expected: 6 dòng `OK`. Có `DIFF` → dừng, so sánh bằng `git diff --no-index`, không tiếp tục.

- [ ] **Step 3: Di chuyển report v1 bằng `git mv` (giữ history)**

```powershell
git mv docs/verify-legacy-phases-report.md docs/superpowers/reports/2026-10-14-verify-legacy-phases.md
```

- [ ] **Step 4: Viết report tổng hợp cho đợt P0 (A1–A10)**

Tạo `docs/superpowers/reports/2026-10-18-ecommerce-v2-core-commerce.md` với **đúng** các mục sau (nội dung tiếng Việt, không bịa thêm số liệu ngoài bảng):

```markdown
# Report tổng hợp — Phase 10 P0 (Task A1–A10)

> Tài liệu **tổng hợp**, không phải report gốc của SDD. Nguồn:
> `.superpowers/sdd/2026-10-18-ecommerce-v2-core-commerce/{progress.md,task-A*-report.md}` + `git log`.
> Report gốc nằm ngoài git (`.superpowers/sdd/` bị ignore cục bộ).

- **Plan:** `docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md`
- **Base:** `44a11f6` → **HEAD:** `8eb0ef3`
- **Test:** 114 pass khi kết thúc đợt (tiếp tục lên 130 sau đợt review fixes — xem report riêng).

## Kết quả theo task

| Task | Nội dung | Commit | Ghi chú |
|---|---|---|---|
| A1 | Thông tin giao hàng trên `Order` + migration | `229a62e` | 4 trường shipping, NOT NULL có default |
| A2 | `ShippingDetails` + `CheckoutViewModel`, đổi chữ ký service | `147de0e` | 4 call site cũ được cập nhật |
| A3 | Nối form checkout vào ViewModel | `44090c4` | kèm fix route view `b04d375` |
| A4 | Hiển thị thông tin nhận hàng ở 2 trang chi tiết | `60da79c` | |
| A5 | `Product.ImageUrl` + quản lý ở Admin | `05049a3` | có placeholder SVG |
| A6 | Truyền `ImageUrl` xuyên catalog/cart/checkout | `6a345b3` | bỏ ảnh hardcode |
| A7 | `PaymentTransaction` + abstraction gateway | `7e44bf1` | unique index `(Provider, TransactionRef)` |
| A8 | `VnPayGateway` + `CodPaymentGateway` | `8d76e57` | verify chữ ký VNPay |
| A9 | `PaymentService` idempotent + chuyển trạng thái | `887e3b8` | callback không hồi sinh đơn huỷ |
| A10 | UI chọn phương thức + return/IPN + trang kết quả | `8eb0ef3` | COD end-to-end |

## Ghi chú

- Đợt review sau A (7 lỗi self-review) tách riêng: `2026-10-18-phase-a-review-fixes.md`.
- B1–B5 (P1) có plan và report riêng trong cùng thư mục.
```

- [ ] **Step 5: Kiểm tra encoding + link nội bộ**

```powershell
Get-ChildItem docs/superpowers/reports/*.md | ForEach-Object {
  $bytes = [IO.File]::ReadAllBytes($_.FullName)
  $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
  $hasReplacement = [Text.Encoding]::UTF8.GetString($bytes).Contains([char]0xFFFD)
  "{0,-58} bom={1} mojibake={2}" -f $_.Name, $bom, $hasReplacement
}
```

Expected: mọi dòng `bom=False mojibake=False`.

- [ ] **Step 6: Graph gate**

Run: `node .gitnexus/run.cjs detect-changes --scope all`
Expected: chỉ file `docs/**`; risk `low`; không `partial`/`truncated`. Thấy file code → STOP.

- [ ] **Step 7: Baseline hoá plan/spec đang untracked (để `git diff` ở Task 2 soát được)**

12 file docs hiện **chưa được git track**: 8 plan trong `docs/superpowers/plans/` (7 plan v2 + plan này) và 5 spec trong `docs/superpowers/specs/`. Nếu không commit baseline, `git diff` ở Task 2 Step 4 sẽ **rỗng** và không chứng minh được gì.

```bash
git status --short -- docs/superpowers/plans docs/superpowers/specs
git add docs/superpowers/plans docs/superpowers/specs
git status --short -- docs/superpowers/plans docs/superpowers/specs   # phải rỗng sau khi add
```

Expected: lệnh `git status --short` đầu tiên liệt kê đúng 8 dòng `??` cho plans và các file `??` cho specs; sau `git add` phải rỗng.

- [ ] **Step 8: Commit archive + baseline trong một commit**

```bash
git add docs/superpowers/reports
git commit -m "docs: archive phase reports and baseline plan/spec docs"
```

`docs/verify-legacy-phases-report.md` đã được `git mv` stage sẵn ở Step 3, không đưa lại vào `git add` (path đó không còn tồn tại).

Gộp archive và baseline vào **một** commit (không phải hai) để Task 2 Step 4 soát được `git diff` chỉ còn thay đổi tick/banner. Tổng số commit của plan: **6** (Task 1, 2, 3, 4, 5, 6 mỗi task một commit).

---

### Task 2: Banner trạng thái + tick hộp kiểm hồi tố cho plan đã xong

**Files:**
- Modify: 9 plan trong `docs/superpowers/plans/` (trừ chính plan này)

**Interfaces:**
- Consumes: đường dẫn report do Task 1 tạo.
- Produces: mỗi plan có dòng `**Trạng thái:**` mà Task 3/4 có thể trích dẫn; số hộp `[ ]` còn lại = 0 ở 8 plan đã xong.

- [ ] **Step 1: Chèn banner ngay sau dòng H1 của từng plan**

Dùng `edit` tool: `oldString` = dòng H1 hiện có, `newString` = dòng H1 + một dòng trống + banner (mỗi file một banner, chép nguyên văn):

~~~text
2026-10-14-verify-legacy-phases.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-14 · commit `9256977..a1251cb` · **24/24 test** · báo cáo: [`docs/superpowers/reports/2026-10-14-verify-legacy-phases.md`](../reports/2026-10-14-verify-legacy-phases.md) · hộp kiểm được tick hồi tố ngày 2026-10-27 từ commit + báo cáo.

2026-10-16-render-free-sqlite-deploy.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-16/17 · commit `abb7afa..44a11f6` · báo cáo: không có (bằng chứng: commit + `/healthz`) · hộp kiểm được tick hồi tố ngày 2026-10-27 từ commit.

2026-10-18-ecommerce-v2-core-commerce.md
> **Trạng thái:** ✅ Hoàn thành phần P0 (Task A1–A10) — 2026-10-18 · commit `229a62e..8eb0ef3` · **114 test** · báo cáo tổng hợp: [`docs/superpowers/reports/2026-10-18-ecommerce-v2-core-commerce.md`](../reports/2026-10-18-ecommerce-v2-core-commerce.md). P1 (B1–B5) đã xong theo plan riêng; P2 (C1–C5) **chưa lập plan** — các mục C trong file này chỉ là dàn ý, không phải cam kết. · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-18-phase-a-review-fixes.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-18 · commit `f6e991d..7e149a6` · **130 test** · báo cáo: [`docs/superpowers/reports/2026-10-18-phase-a-review-fixes.md`](../reports/2026-10-18-phase-a-review-fixes.md) · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-19-phase-b1-email-flows.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-19 · commit `9011743..ac5b1ea` · **159 test** · báo cáo: [`docs/superpowers/reports/2026-10-19-phase-b1-email-flows.md`](../reports/2026-10-19-phase-b1-email-flows.md) · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-19-phase-b2-profile-address-book.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-19 · commit `e04ac31..c4afdc6` · **178 test** · báo cáo: [`docs/superpowers/reports/2026-10-19-phase-b2-profile-address-book.md`](../reports/2026-10-19-phase-b2-profile-address-book.md) · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-19-phase-b3-coupons.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-19 · commit `910b49d..8f89825` · **206 test** · báo cáo: [`docs/superpowers/reports/2026-10-19-phase-b3-coupons.md`](../reports/2026-10-19-phase-b3-coupons.md) · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-22-phase-b4-reviews.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-22 · commit `865ce74..1f35832` · **241 test** · báo cáo: [`docs/superpowers/reports/2026-10-22-phase-b4-reviews.md`](../reports/2026-10-22-phase-b4-reviews.md) · hộp kiểm được tick hồi tố ngày 2026-10-27.

2026-10-22-phase-b5-shipping-tax.md
> **Trạng thái:** ✅ Hoàn thành — 2026-10-22 · commit `bfaa5f7..46edad9` · **266 test** · báo cáo: [`docs/superpowers/reports/2026-10-22-phase-b5-shipping-tax.md`](../reports/2026-10-22-phase-b5-shipping-tax.md) · hộp kiểm đã tick trong lúc thực thi.
~~~

- [ ] **Step 2: Tick hồi tố — chỉ dòng bắt đầu bằng `- [ ]`**

```powershell
$files = @(
  'docs/superpowers/plans/2026-10-14-verify-legacy-phases.md',
  'docs/superpowers/plans/2026-10-16-render-free-sqlite-deploy.md',
  'docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md',
  'docs/superpowers/plans/2026-10-18-phase-a-review-fixes.md',
  'docs/superpowers/plans/2026-10-19-phase-b1-email-flows.md',
  'docs/superpowers/plans/2026-10-19-phase-b2-profile-address-book.md',
  'docs/superpowers/plans/2026-10-19-phase-b3-coupons.md',
  'docs/superpowers/plans/2026-10-22-phase-b4-reviews.md'
)
foreach ($f in $files) {
  $text = [IO.File]::ReadAllText($f)
  $new  = [regex]::Replace($text, '(?m)^(\s*)- \[ \]', '$1- [x]')
  if ($new -ne $text) {
    [IO.File]::WriteAllText($f, $new, [Text.UTF8Encoding]::new($false))
  }
}
```

- [ ] **Step 3: Xác nhận số hộp còn lại = 0 và dòng mẫu còn nguyên**

```powershell
Get-ChildItem docs/superpowers/plans/*.md | ForEach-Object {
  $lines = Get-Content $_.FullName
  $todo = ($lines | Where-Object { $_ -match '^\s*-\s*\[ \]' }).Count
  $sample = ($lines | Where-Object { $_ -match 'Steps use checkbox' -and $_ -match '\(`- \[ \]`\)' }).Count
  "{0,-52} todo={1} sampleLineOk={2}" -f $_.Name, $todo, $sample
}
```

Expected: 8 file đã xong có `todo=0`; `2026-10-27-docs-debt-cleanup.md` chưa tick nên `todo>0` (sẽ tick ở Task 6); `sampleLineOk=1` cho 7 file có dòng mẫu, `0` cho B4/B5.

- [ ] **Step 4: Soát diff — chỉ có tick + banner, không đổi câu chữ**

Diff này chỉ soát được **vì** Task 1 đã commit baseline 8 plan; nếu Task 1 chưa xong thì lệnh dưới trả rỗng và bước này vô nghĩa.

```powershell
git diff -U0 -- docs/superpowers/plans | Select-String '^[+-](?!\+\+|--)' | Select-String -NotMatch '^\s*[+-]\s*-\s*\[[ x]\]|^\+\s*>|^\+\s*$'
```

Expected: không còn dòng nào (mọi thay đổi đều là hộp kiểm, banner, hoặc dòng trống). Có dòng khác → `git checkout -- <file>` file đó và làm lại bằng tay.

- [ ] **Step 5: Graph gate**

Run: `node .gitnexus/run.cjs detect-changes --scope all`
Expected: chỉ `docs/**`, risk `low`.

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/plans/2026-10-14-verify-legacy-phases.md docs/superpowers/plans/2026-10-16-render-free-sqlite-deploy.md docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md docs/superpowers/plans/2026-10-18-phase-a-review-fixes.md docs/superpowers/plans/2026-10-19-phase-b1-email-flows.md docs/superpowers/plans/2026-10-19-phase-b2-profile-address-book.md docs/superpowers/plans/2026-10-19-phase-b3-coupons.md docs/superpowers/plans/2026-10-22-phase-b4-reviews.md docs/superpowers/plans/2026-10-22-phase-b5-shipping-tax.md
git commit -m "docs(plans): mark completed phases with status and ticked steps"
```

---

### Task 3: Roadmap thành nguồn chân lý cho tiến độ

**Files:**
- Modify: `docs/roadmap.md`

**Interfaces:**
- Consumes: report path (Task 1), banner trạng thái (Task 2).
- Produces: trạng thái tiến độ duy nhất Task 4 trích dẫn; bảng nhật ký cho người đọc không cần mở git log.

- [ ] **Step 1: Thêm khối trạng thái ngay sau `# E-Commerce Roadmap`**

```markdown
> **Cập nhật lần cuối:** 2026-10-27 · nhánh `main` @ `46edad9` · test `266/266` PASS · production: <https://ecommerce-demo-9phe.onrender.com>
>
> Đây là **nguồn chân lý duy nhất cho tiến độ**. Bản đồ tài liệu (spec / plan / report, quy ước cập nhật) ở [`docs/README.md`](README.md).
```

- [ ] **Step 2: Sửa 3 chỗ sai/lỗi thời đã xác minh (định vị bằng nội dung, không bằng số dòng)**

1. Bullet Phase 8.5 nói sai tên file. Thay **nguyên dòng**:

   ~~~text
   Cũ:
     - Tích hợp partial `_LoginPartial.cshtml` và hiển thị số lượng giỏ hàng thực tế động (`ICartService`)

   Mới:
     - Tích hợp partial `_LoginPartial.cshtml` cho liên kết tài khoản; số lượng giỏ hàng lấy động từ `ICartService` trong `_Layout.cshtml` (dòng 4 + 99–104).
   ~~~

   Bằng chứng: `ECommerce.Web/Views/Shared/_Layout.cshtml:1` inject `ICartService`, `:4` gọi `GetCartItemCountAsync()`, `:99-104` render badge.
2. Dòng kết thúc Phase 8.5 (`Đảm bảo logic nghiệp vụ và 23/23 unit tests giữ vững 100%`) → giữ nguyên câu, thêm hậu tố:
   ` (số liệu tại thời điểm Phase 8.5; suite hiện tại 266/266 — xem đầu file)`
3. Dòng kết thúc Phase 8.6 (`... chạy lại 23/23 unit tests`) → thêm hậu tố:
   ` (23/23 tại thời điểm Phase 8.6; hiện 266/266)`

- [ ] **Step 3: Tick P0/P1 và gắn mã task**

Thay 10 dòng `- [ ]` trong hai mục P0/P1 thành `- [x]`, giữ nguyên câu chữ, thêm hậu tố mã task:

- P0: `· A1–A3`, `· A4`, `· A5–A6`, `· A7–A10`, `· A9–A10` (theo đúng thứ tự 5 bullet hiện có).
- P1: `· B1`, `· B2`, `· B3`, `· B4`, `· B5`.
- P2: **giữ nguyên 5 dòng `- [ ]`** (chưa làm).

- [ ] **Step 4: Thêm bảng nhật ký thực thi ngay dưới mục P2**

```markdown
### Nhật ký thực thi (v1 + v2)

| Đợt | Plan | Báo cáo | Commit | Test sau đợt |
|---|---|---|---|---|
| Verify v1 (Phase 0–8.6) | `superpowers/plans/2026-10-14-verify-legacy-phases.md` | [`reports/2026-10-14-verify-legacy-phases.md`](superpowers/reports/2026-10-14-verify-legacy-phases.md) | `9256977..a1251cb` | 24/24 |
| Deploy Render | `superpowers/plans/2026-10-16-render-free-sqlite-deploy.md` | — (commit + `/healthz`) | `abb7afa..44a11f6` | — |
| v2 P0 (A1–A10) | `superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md` | [`reports/2026-10-18-ecommerce-v2-core-commerce.md`](superpowers/reports/2026-10-18-ecommerce-v2-core-commerce.md) | `229a62e..8eb0ef3` | 114 |
| v2 P0 review fixes | `superpowers/plans/2026-10-18-phase-a-review-fixes.md` | [`reports/2026-10-18-phase-a-review-fixes.md`](superpowers/reports/2026-10-18-phase-a-review-fixes.md) | `f6e991d..7e149a6` | 130 |
| v2 P1 — email (B1) | `superpowers/plans/2026-10-19-phase-b1-email-flows.md` | [`reports/2026-10-19-phase-b1-email-flows.md`](superpowers/reports/2026-10-19-phase-b1-email-flows.md) | `9011743..ac5b1ea` | 159 |
| v2 P1 — profile (B2) | `superpowers/plans/2026-10-19-phase-b2-profile-address-book.md` | [`reports/2026-10-19-phase-b2-profile-address-book.md`](superpowers/reports/2026-10-19-phase-b2-profile-address-book.md) | `e04ac31..c4afdc6` | 178 |
| v2 P1 — coupon (B3) | `superpowers/plans/2026-10-19-phase-b3-coupons.md` | [`reports/2026-10-19-phase-b3-coupons.md`](superpowers/reports/2026-10-19-phase-b3-coupons.md) | `910b49d..8f89825` | 206 |
| v2 P1 — reviews (B4) | `superpowers/plans/2026-10-22-phase-b4-reviews.md` | [`reports/2026-10-22-phase-b4-reviews.md`](superpowers/reports/2026-10-22-phase-b4-reviews.md) | `865ce74..1f35832` | 241 |
| v2 P1 — shipping & tax (B5) | `superpowers/plans/2026-10-22-phase-b5-shipping-tax.md` | [`reports/2026-10-22-phase-b5-shipping-tax.md`](superpowers/reports/2026-10-22-phase-b5-shipping-tax.md) | `bfaa5f7..46edad9` | 266 |
```

- [ ] **Step 5: Kiểm tra bằng máy**

```powershell
$text = [IO.File]::ReadAllText('docs/roadmap.md')
$all  = [regex]::Matches($text, '(?m)^\s*-\s*\[ \]').Count
$p2   = $text.Substring($text.IndexOf('### P2'))
$p2n  = [regex]::Matches($p2, '(?m)^\s*-\s*\[ \]').Count
"Tổng ô trống: {0}" -f $all        # mong đợi: 5
"Mục P2: {0}" -f $p2n             # mong đợi: 5
"P0/P1 còn trống: {0}" -f ($all - $p2n)   # mong đợi: 0
```

Expected: `5`, `5`, `0`.

- [ ] **Step 6: Kiểm tra production trước khi khẳng định**

```powershell
curl.exe -s -o NUL -w "%{http_code}`n" --max-time 60 https://ecommerce-demo-9phe.onrender.com/healthz
```

Expected: `200`. Nếu khác: **không sửa số liệu khác**, đổi dòng production trong khối trạng thái thành `production: https://ecommerce-demo-9phe.onrender.com (kiểm tra lần cuối 2026-10-22; `/healthz` không phản hồi ngày 2026-10-27)` và ghi lại mã lỗi/kết quả vào report Task 6.

- [ ] **Step 7: Graph gate + commit**

```bash
node .gitnexus/run.cjs detect-changes --scope all
git add docs/roadmap.md
git commit -m "docs(roadmap): sync Phase 10 progress with shipped code"
```

Lưu ý: `docs/roadmap.md` đang có sẵn thay đổi **chưa commit** của người dùng (thêm nguyên mục `## Phase 10`). Commit này gộp luôn phần đó — đúng ý định, vì phase 10 chính là nội dung đang được đồng bộ trạng thái. Không `git checkout --` file này.

---

### Task 4: `docs/README.md` — bản đồ tài liệu + checklist đóng phase

**Files:**
- Create: `docs/README.md`

**Interfaces:**
- Consumes: cấu trúc `docs/` sau Task 1–3.
- Produces: điểm vào duy nhất cho người mới và cho agent; quy trình chống tái phát nợ.

- [ ] **Step 1: Viết file với đúng 6 mục sau**

```markdown
# Tài liệu dự án ECommerce.Web

Một chỗ để biết **đọc gì**, **tin gì**, và **cập nhật gì** sau mỗi đợt làm việc.

## 1. Đọc gì khi nào

| Câu hỏi | Đọc |
|---|---|
| Dự án đang ở đâu? | [`roadmap.md`](roadmap.md) — nguồn chân lý duy nhất cho tiến độ |
| Yêu cầu gốc của khách? | [`01-Project-Overview-E-Commerce.docx`](01-Project-Overview-E-Commerce.docx) |
| Vì sao thiết kế như vậy? | [`superpowers/specs/`](superpowers/specs/) — mỗi phase P1 có một design doc |
| Đợt đó đã làm từng bước thế nào? | [`superpowers/plans/`](superpowers/plans/) — plan + hộp kiểm đã tick |
| Có bằng chứng kiểm chứng không? | [`superpowers/reports/`](superpowers/reports/) — test, live verify, mutation check, rủi ro còn lại |
| Template giao diện gốc? | `../e_commerce_app.html`, `../qu_n_tr_e_commerce_app.html` (chỉ tham khảo, không build) |

## 2. Trạng thái hiện tại

| Hạng mục | Giá trị |
|---|---|
| Nhánh / commit | `main` @ `46edad9` (2026-10-22) |
| Test | 266/266 PASS (`dotnet test`) |
| Production | https://ecommerce-demo-9phe.onrender.com |
| Đã xong | v1 (Phase 0–9), v2 P0 (A1–A10 + review fixes), v2 P1 (B1–B5) |
| Chưa làm | v2 P2: catalog nâng cao, wishlist, reporting/export, audit log, sản phẩm liên quan |
| Nợ đã biết | upload ảnh thật (Render free dùng disk tạm), keepalive cold-start, background jobs, caching, CI/CD |

> Snapshot này chỉ để đọc nhanh; số liệu chuẩn nằm ở `roadmap.md`.

## 3. Cấu trúc

```text
docs/
  01-Project-Overview-E-Commerce.docx   # yêu cầu gốc
  README.md                             # file này
  roadmap.md                            # tiến độ (source of truth)
  superpowers/
    plans/    YYYY-MM-DD-<topic>.md     # kế hoạch thực thi, có hộp kiểm
    specs/    YYYY-MM-DD-<topic>-design.md
    reports/  YYYY-MM-DD-<phase>.md     # bằng chứng sau khi xong
```

## 4. Quy ước

- **Tên file:** `YYYY-MM-DD-<topic>-design.md` cho spec, `YYYY-MM-DD-<topic>.md` cho plan và report.
- **Từ vựng trạng thái:** `⬜ Chưa làm` · `🚧 Đang làm` · `✅ Hoàn thành` · `⏸ Tạm dừng` · `❌ Huỷ`.
- **Hộp kiểm:** trong plan là nơi theo dõi thực thi. Report/plan cũ được tick hồi tố thì banner phải ghi rõ ngày tick và nguồn bằng chứng.
- **Banner đầu plan** (bắt buộc với plan đã xong):
  > **Trạng thái:** ✅ Hoàn thành — <ngày> · commit `<range>` · <N>/<N> test · báo cáo: [link]
- **Ngôn ngữ:** tiếng Việt, giữ thuật ngữ kỹ thuật tiếng Anh. Không dịch lại tài liệu cũ.
- **Encoding:** UTF-8, không BOM, xuống dòng LF.
- **Report gốc** nằm ở `.superpowers/sdd/<phase>/` (không commit). Bản lưu trong git là **bản sao hoặc bản tổng hợp**, ghi rõ nguồn ở đầu file.

## 5. Quy trình đóng phase (bắt buộc)

1. Chạy `dotnet test` + live verify theo plan; ghi lại output thật.
2. Viết/khôi phục report trong `.superpowers/sdd/<phase>/`.
3. Copy report sang `docs/superpowers/reports/YYYY-MM-DD-<phase>.md` (verbatim, hoặc bản tổng hợp có ghi nguồn).
4. Tick hộp kiểm trong plan + thêm banner trạng thái.
5. Cập nhật `roadmap.md`: tick mục tương ứng, thêm dòng vào **Nhật ký thực thi**, cập nhật khối trạng thái đầu file.
6. Commit riêng phần docs: `git add docs/...` (không `-A`), chạy `node .gitnexus/run.cjs detect-changes --scope all` trước khi commit.

## 6. Ngoài phạm vi

- `.superpowers/sdd/**` — working artifact của agent, bị ignore cục bộ, không commit.
- `.worktrees/**` — worktree tạm.
- `specs/` ở root — đã xoá (placeholder không dùng; Playwright dùng `./tests`).
- Chuyển đổi line-ending toàn repo (`.gitattributes` + renormalize) — việc riêng, không trộn vào commit tài liệu.
```

- [ ] **Step 2: Kiểm tra link tương đối trong `docs/**/*.md` resolve (bỏ qua code fence)**

```powershell
$bad = @()
Get-ChildItem docs -Recurse -Filter *.md | ForEach-Object {
  $dir = $_.DirectoryName; $name = $_.Name; $inFence = $false; $i = 0
  foreach ($line in [IO.File]::ReadAllLines($_.FullName)) {
    $i++
    $trim = $line.TrimStart()
    if ($trim.StartsWith('```') -or $trim.StartsWith('~~~')) { $inFence = -not $inFence; continue }
    if ($inFence) { continue }
    foreach ($m in [regex]::Matches($line, '\]\(([^)\s]+)\)')) {
      $t = $m.Groups[1].Value
      if ($t -match '^(https?:|mailto:|#)') { continue }
      $p = Join-Path $dir $t.Split('#')[0]
      if (-not (Test-Path $p)) { $bad += "$name`:$i -> $t" }
    }
  }
}
if ($bad.Count) { $bad; exit 1 } else { "OK: mọi link tương đối ngoài code fence trong docs/ đều resolve" }
```

Expected: `OK`. Có `->` nào → sửa link rồi chạy lại. Đây là bước chặn cho Review Focus 1.

- [ ] **Step 3: Graph gate + commit**

```bash
node .gitnexus/run.cjs detect-changes --scope all
git add docs/README.md
git commit -m "docs: add documentation index and closeout checklist"
```

---

### Task 5: Xoá placeholder `specs/` ở root

**Files:**
- Delete: `specs/README.md` (tracked từ `9256977`), thư mục `specs/`

**Interfaces:**
- Consumes: kết quả kiểm tra "không ai tham chiếu `specs/`".
- Produces: root gọn, không còn thư mục gây nhầm với `docs/superpowers/specs/`; 1 commit xoá file.

- [ ] **Step 1: Chứng minh không có cấu hình/tài liệu nào trỏ tới `specs/`**

```powershell
# 1) Không tooling/cấu hình nào dùng specs/
Select-String -Path package.json,playwright.config.ts,.gitignore -Pattern 'specs/'
# 2) File trong specs/ được git track những gì (đã track từ Initial commit → xoá sẽ có diff)
git ls-files specs
# 3) Không tài liệu nào tham chiếu specs/ ở root (bỏ qua chính plan này + README sẽ viết ở Task 4)
Get-ChildItem docs -Recurse -Filter *.md |
  Where-Object { $_.Name -ne 'README.md' -and $_.Name -ne '2026-10-27-docs-debt-cleanup.md' } |
  Select-String -Pattern '(?<!superpowers/)specs/'
```

Expected: lệnh 1 và 3 **rỗng**; lệnh 2 trả đúng một dòng `specs/README.md` (đã track từ `9256977`, nên Task 5 **cần** một commit). Có kết quả ở lệnh 1/3 → dừng, báo người dùng.

- [ ] **Step 2: Xác nhận Playwright không dùng thư mục này**

Run: `Select-String -Path playwright.config.ts -Pattern 'testDir'`
Expected: `testDir: './tests'`.

- [ ] **Step 3: Xoá bằng `git rm` (file đang được track)**

```powershell
git rm specs/README.md
if (Test-Path specs) { Remove-Item specs -Recurse -Force }   # chỉ còn thư mục rỗng sau git rm
Test-Path specs                                              # False
git status --short specs                                     # D  specs/README.md
```

- [ ] **Step 4: Graph gate + commit**

```bash
node .gitnexus/run.cjs detect-changes --scope all
git commit -m "chore(docs): remove stray specs placeholder"
```

Graph gate kỳ vọng section `specs/README.md` (docs-only, risk `low`); thấy file code → STOP.

- [ ] **Step 5: Xác nhận lại**

```powershell
git ls-files specs          # rỗng
Test-Path specs             # False
git status --short          # không còn dòng nào chứa specs/
```

---

### Task 6: Quét kiểm chứng toàn bộ + report

**Files:**
- Create: `docs/superpowers/reports/2026-10-27-docs-debt-cleanup.md`
- Modify: plan này (tick hộp kiểm khi thực thi)

**Interfaces:**
- Consumes: toàn bộ Task 1–5.
- Produces: bằng chứng cuối cùng rằng tài liệu khớp code; là mẫu cho checklist đóng phase.

- [ ] **Step 1: Chạy full test suite**

Run: `dotnet test --nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 266, Skipped: 0, Total: 266`. Khác → dừng, báo (có thể môi trường, không phải docs).

- [ ] **Step 2: Kiểm chứng mọi commit SHA trong tài liệu tồn tại**

```powershell
$files = Get-ChildItem docs -Recurse -Filter *.md
$shas = foreach ($f in $files) {
  $t = [IO.File]::ReadAllText($f.FullName)
  # chỉ lấy SHA nằm gọn trong cặp backtick (tránh bắt tên migration như `20260925125612_InitialCreate`)
  foreach ($m in [regex]::Matches($t, '`([0-9a-f]{7,40})(?:\.\.([0-9a-f]{7,40}))?`')) {
    $m.Groups[1].Value
    if ($m.Groups[2].Success) { $m.Groups[2].Value }
  }
}
$shas = $shas | Sort-Object -Unique
$missing = @()
foreach ($s in $shas) {
  git cat-file -e "$s^{commit}" 2>$null
  if ($LASTEXITCODE -ne 0) { $missing += $s }
}
if ($missing) { "THIẾU: $($missing -join ', ')"; exit 1 } else { "OK: $($shas.Count) SHA đều tồn tại" }
```

Expected: `OK: <n> SHA đều tồn tại`. SHA thiếu → sửa tài liệu hoặc bỏ khẳng định đó, không tick. Nếu SHA "thiếu" thực ra là số hiệu migration/ID, siết regex lại chứ **không** sửa tài liệu cho vừa regex.

- [ ] **Step 3: Kiểm tra tính nhất quán tiến độ**

```powershell
# 1) roadmap chỉ còn 5 ô trống = 5 mục P2
$roadmap = [IO.File]::ReadAllText('docs/roadmap.md')
"roadmap unchecked = " + ([regex]::Matches($roadmap, '(?m)^\s*-\s*\[ \]')).Count   # 5

# 2) 8 plan đã xong: 0 ô trống
Get-ChildItem docs/superpowers/plans/*.md | Where-Object { $_.Name -ne '2026-10-27-docs-debt-cleanup.md' } |
  ForEach-Object { "{0} todo={1}" -f $_.Name, (([regex]::Matches([IO.File]::ReadAllText($_.FullName), '(?m)^\s*-\s*\[ \]')).Count) }

# 3) mọi plan đã xong có banner trạng thái
Get-ChildItem docs/superpowers/plans/*.md | Where-Object { $_.Name -ne '2026-10-27-docs-debt-cleanup.md' } |
  ForEach-Object { "{0} banner={1}" -f $_.Name, ([IO.File]::ReadAllText($_.FullName).Contains('**Trạng thái:**')) }
```

Expected: `roadmap unchecked = 5`; mọi plan `todo=0 banner=True`. Dùng `[IO.File]::ReadAllText` (không `Get-Content -Raw`) để không phụ thuộc encoding mặc định của PowerShell.

- [ ] **Step 4: Kiểm tra chỉ docs bị đổi so với `46edad9`**

```powershell
git diff --name-only 46edad9..HEAD
```

Expected: mọi dòng bắt đầu bằng `docs/`, cộng đúng một dòng `specs/README.md` (Task 5). Có file khác → dừng, điều tra.

- [ ] **Step 5: Kiểm tra không còn nợ "untracked docs"**

Run: `git status --short -- docs`
Expected: rỗng — mọi thay đổi docs đã commit ở Task 1–4; report của Task 6 chưa được tạo ở bước này. Có dòng `??`/` M` → điều tra trước khi tiếp tục.

- [ ] **Step 6: Viết report kiểm chứng**

Tạo `docs/superpowers/reports/2026-10-27-docs-debt-cleanup.md` với các mục: bảng thay đổi theo task (file + commit), output thật của Step 1–5, danh sách kiểm tra Review Focus 1–5 (kèm bằng chứng từng mục), và mục "Nợ còn lại" ghi rõ: P2 chưa làm, report deploy không tồn tại, nhiễu CRLF chưa xử lý.

- [ ] **Step 7: Tick toàn bộ hộp kiểm trong plan này + chạy lại 3 chốt chặn**

```powershell
$p = 'docs/superpowers/plans/2026-10-27-docs-debt-cleanup.md'
$text = [IO.File]::ReadAllText($p)
$new  = [regex]::Replace($text, '(?m)^(\s*)- \[ \]', '$1- [x]')
if ($new -ne $text) { [IO.File]::WriteAllText($p, $new, [Text.UTF8Encoding]::new($false)) }
"todo còn lại = " + ([regex]::Matches([IO.File]::ReadAllText($p), '(?m)^\s*-\s*\[ \]')).Count   # 0
"dòng mẫu còn nguyên = " + ([regex]::Matches([IO.File]::ReadAllText($p), '\(`- \[ \]`\)')).Count   # 1
```

Sau khi tick, chạy lại **nguyên** script link ở Task 4 Step 2 (fence-aware) và script SHA ở Step 2 trên — tick không được làm hỏng link hay SHA. Cả hai phải vẫn `OK`.

- [ ] **Step 8: Graph gate + commit cuối**

```bash
node .gitnexus/run.cjs detect-changes --scope all
git add docs/superpowers/reports/2026-10-27-docs-debt-cleanup.md docs/superpowers/plans/2026-10-27-docs-debt-cleanup.md
git commit -m "docs: add docs cleanup verification report"
```

- [ ] **Step 9: Báo cáo cho người dùng**

In ra: 6 commit mới (`git log --oneline -6`), số ô trống còn lại theo từng file, kết quả test, kết quả healthz, và 3 việc tiếp theo đề xuất (P2/C1, `.gitattributes`, report deploy còn thiếu).

---

## Self-review notes

- **Spec coverage:** yêu cầu "dọn nợ tài liệu kĩ" được phủ bởi 5 nhóm nợ đã kiểm ở §Hiện trạng: report ngoài git (Task 1), plan không phản ánh trạng thái (Task 2), roadmap sai/lỗi thời (Task 3), thiếu điểm vào + quy trình chống tái phát (Task 4), file rác (Task 5), và kiểm chứng cuối (Task 6).
- **Quyết định của tôi (người đọc kiểm lại):**
  1. Tick hồi tố toàn bộ hộp kiểm của 8 plan đã xong thay vì chỉ thêm banner — vì `[ ]` trơ trọi gây hiểu sai; banner ghi rõ ngày tick + nguồn.
  2. Giữ report gốc trong `.superpowers/sdd/`, chỉ đưa bản sao/bản tổng hợp vào git — tránh commit 26 file brief/test log.
  3. Report đợt deploy Render **không được tạo lại** vì không có nguồn; ghi "không có report" thay vì bịa.
  4. Không xoá `docs/01-Project-Overview-E-Commerce.docx` dù chỉ 420 byte — là yêu cầu gốc, còn giá trị tham chiếu.
  5. Không gộp `.gitattributes`/renormalize vào đây để commit tài liệu sạch, dễ review.
- **Rủi ro đã biết:** Task 2 Step 2 dùng regex thay thế hàng loạt; Step 3/4 là chốt chặn (đếm + diff). Nếu diff lộ dòng không phải hộp kiểm → `git checkout --` file đó và làm lại bằng tay.

## Ngoài phạm vi (không làm trong plan này)

- Lập plan cho P2 (C1–C5).
- Thêm `.gitattributes` + `git add --renormalize .` (nhiễu CRLF ở `.agents/`, `wwwroot/lib/`, `site.css`, `site.js`, `.slnx`, `launchSettings.json`).
- Sửa `opencode.json`, `.claude/`, `.agents/`, `agent/`.
- Dịch tài liệu sang tiếng Anh hoặc thống nhất ngôn ngữ.
- Xoá 2 file HTML template ở root.
