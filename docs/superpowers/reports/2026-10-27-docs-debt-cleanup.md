# Report kiểm chứng — Dọn nợ tài liệu (docs debt cleanup)

- **Plan:** `docs/superpowers/plans/2026-10-27-docs-debt-cleanup.md`
- **Ngày:** 2026-10-27
- **Branch/HEAD trước:** `main` @ `46edad9`
- **Chế độ:** inline (executing-plans), không worktree

## Thay đổi theo task

| Task | Commit | Thay đổi |
|---|---|---|
| 1 | `f87ad93` | Thêm 6 report verbatim + 1 report tổng hợp P0 vào `docs/superpowers/reports/`, `git mv` `docs/verify-legacy-phases-report.md` → `docs/superpowers/reports/2026-10-14-verify-legacy-phases.md`, baseline 8 plan + 5 spec vào git |
| 2 | `4208baf` | Thêm banner `**Trạng thái:**` + tick hồi tố 292 hộp cho 8 plan đã xong (chỉ đầu dòng `- [ ]`), giữ nguyên dòng mẫu |
| 3 | `510204a` | Đồng bộ `docs/roadmap.md`: khối trạng thái đầu file, sửa 3 chỗ sai, tick 10 hộp P0/P1, thêm bảng nhật ký thực thi |
| 4 | `897a5d7` | Thêm `docs/README.md` (bản đồ tài liệu + quy ước + checklist đóng phase) |
| 5 | `70382cb` | `git rm` `specs/README.md` (đã track từ `9256977`) và xoá thư mục `specs/` |
| 6 | (sẽ commit) | Viết báo cáo này, tick plan |

**Tổng commit docs liên quan:** 6 commit sau `46edad9` (bao gồm cả report này).

## Kiểm chứng (output thật)

| Bước | Kết quả |
|---|---|
| `dotnet test --nologo -v q` | Passed! - Failed: 0, Passed: 266, Skipped: 0, Total: 266 |
| `/healthz` production | `200` (https://ecommerce-demo-9phe.onrender.com/healthz) |
| SHA trong `docs/**/*.md` (siết trong backtick) | OK: 45 SHA đều tồn tại |
| `docs/roadmap.md` | `roadmap unchecked=5` (chỉ 5 mục P2 còn lại) |
| Plan đã hoàn thành (8 file) | `todo=0 banner=True` cho tất cả |
| `git diff --name-only 46edad9..HEAD` | Chỉ `docs/**` + `specs/README.md` (xóa) — khớp với Task 5 |
| Link tương đối ngoài code fence | ALL OK |

## Review Focus

| Mục | Trạng thái | Bằng chứng |
|---|---|---|
| Link tương đối hỏng | PASS | Checker fence-aware chạy trên toàn bộ `docs/**/*.md` — không phát hiện lỗi |
| Tick sai sự thật | PASS | Chỉ tick dòng bắt đầu `- [ ]`; 7 dòng mẫu cú pháp giữ nguyên `[ ]`; diff chỉ thêm banner + tick |
| Mojibake | PASS | Tất cả report trong `docs/superpowers/reports/` `bom=False mojibake=False` |
| Khẳng định production sai | PASS | `/healthz` trả `200`; roadmap ghi trạng thái rõ ràng |
| Nợ tái phát | PASS | Thêm `docs/README.md` với checklist đóng phase bắt buộc |

## Nợ còn lại

- **P2 chưa làm:** Catalog nâng cao, wishlist, reporting/export, audit log, sản phẩm liên quan (5 mục trong roadmap)
- **Report deploy:** `docs/superpowers/plans/2026-10-16-render-free-sqlite-deploy.md` không có report riêng — ghi "không có report (bằng chứng: commit + `/healthz`)" thay vì bịa
- **Nhiễu CRLF:** Nhiều file `.agents/`, `wwwroot/lib/`, `site.css`, `.slnx` vẫn có thay đổi line-ending theo config git; không xử lý trong plan này (ngoài phạm vi)
- **Report gốc:** Vẫn nằm ở `.superpowers/sdd/` (bị ignore cục bộ), chỉ bản sao/bản tổng hợp trong git
