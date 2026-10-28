# Tài liệu dự án ECommerce.Web

Một chỗ để biết **đọc gì**, **tin gì**, và **cập nhật gì** sau mỗi đợt làm việc.

## 1. Đọc gì khi nào

| Câu hỏi | Đọc |
|---|---|
| Dự án đang ở đâu? | [`roadmap.md`](roadmap.md) — nguồn chân lý duy nhất cho tiến độ |
| Phạm vi đã khóa / không làm gì nữa? | [`scope-lock.md`](scope-lock.md) — quy tắc đóng băng & tiêu chuẩn bản demo portfolio |
| Yêu cầu gốc của khách? | [`01-Project-Overview-E-Commerce.docx`](01-Project-Overview-E-Commerce.docx) |
| Vì sao thiết kế như vậy? | [`superpowers/specs/`](superpowers/specs/) — mỗi phase P1 có một design doc |
| Đợt đó đã làm từng bước thế nào? | [`superpowers/plans/`](superpowers/plans/) — plan + hộp kiểm đã tick |
| Có bằng chứng kiểm chứng không? | [`superpowers/reports/`](superpowers/reports/) — test, live verify, mutation check, rủi ro còn lại |
| Template giao diện gốc? | `../e_commerce_app.html`, `../qu_n_tr_e_commerce_app.html` (chỉ tham khảo, không build) |

## 2. Trạng thái hiện tại

| Hạng mục | Giá trị |
|---|---|
| Nhánh / commit | `main` @ `b78044d` (2026-10-28) |
| Test | 277/277 PASS (`dotnet test`) |
| Production | https://ecommerce-demo-9phe.onrender.com |
| Đã xong | v1 (Phase 0–9), v2 P0 (A1–A10 + review fixes), v2 P1 (B1–B5), Scope Lock (T1–T5) |
| Chưa làm | v2 P2: đóng băng có chủ đích — xem [`scope-lock.md`](scope-lock.md) |
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
