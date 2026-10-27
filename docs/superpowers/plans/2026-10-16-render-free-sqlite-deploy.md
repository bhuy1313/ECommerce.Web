# Kế hoạch triển khai Render Free (Docker) + SQLite

> **Trạng thái:** ✅ Hoàn thành — 2026-10-16/17 · commit `abb7afa..44a11f6` · báo cáo: không có (bằng chứng: commit + `/healthz`) · hộp kiểm được tick hồi tố ngày 2026-10-27 từ commit.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Mục tiêu:** Đưa ECommerce.Web lên Render gói free dưới dạng Docker web service, dùng database SQLite tạm thời phục vụ demo.

**Kiến trúc:** Dockerfile nhiều giai đoạn (build bằng SDK preview, chạy bằng ASP.NET runtime preview). `Program.cs` chọn EF Core provider theo connection string: chứa `.db` → SQLite (+ `EnsureCreated`), ngược lại → SQL Server (+ `Migrate`, giữ nguyên đường dev). File `render.yaml` khai báo blueprint gồm web service free, health check `/healthz` và biến môi trường.

**Công nghệ:** .NET 10 (preview), EF Core 10.0.12 (SqlServer + gói Sqlite mới), Docker (image `mcr.microsoft.com/dotnet/sdk:10.0-preview`, `mcr.microsoft.com/dotnet/aspnet:10.0-preview`), Render Blueprint (`render.yaml`).

**Spec:** Không có tài liệu spec riêng — thiết kế đã chốt trong chat ngày 2026-10-16 (chủ động bỏ qua brainstorming theo yêu cầu). Các quyết định do người lập kế hoạch tự chốt được đánh dấu **[tự chốt]**.

## Ràng buộc chung

- Giữ nguyên target framework `net10.0`; không hạ về LTS.
- Giới hạn brute-force auth mặc định trong code vẫn là 10 req/phút (`RateLimiting:AuthPermitLimit`); không được nới lỏng.
- Đường dev SQL Server (`LocalDB`, `Migrate()`) phải chạy y như cũ — bộ test E2E vẫn dùng đường này.
- Không commit secret vào repo: mật khẩu admin chỉ đi qua env `SeedAdmin__Password`.
- Dữ liệu demo được phép mất: restart có thể seed lại từ đầu (đã chấp thuận, không tính là bug).
- Máy này đã có Docker 29.8.0 và `dotnet-ef` 10.0.12 để kiểm chứng local.

## Điểm cần rà soát (Review Focus)

- Render start container nhưng app nghe sai port (bỏ qua `$PORT`) → service không bao giờ healthy; hiện tượng: boot timeout. (Đã ghim kiểm chứng: Task 2 Bước 5 — lệnh `curl` ở đó chỉ pass nếu `$PORT` được tôn trọng.)
- Tag image nổi `10.0-preview` có thể đổi/gãy giữa các lần chạy → build đột nhiên fail hoặc lệch SDK/runtime. (Đã ghim: Task 1.)
- Hai người demo bấm checkout cùng lúc → lỗi `database is locked` của SQLite khi ghi đồng thời. (Đã ghim: Task 2 Bước 5 — kiểm chứng thêm 2 tab nhanh.)
- Thiếu/trống `SeedAdmin__Password` trên Render → không có tài khoản admin, demo login fail mà không rõ lý do. (Đã ghim: Task 4.)
- Cold start giữa buổi demo (sleep sau 15 phút) làm cú click đầu treo ~60s → người trình bày bị động. (Đã ghim: Task 4, bước runbook, không phải code.)

---

## Cấu trúc file

- Tạo mới: `Dockerfile` — build nhiều giai đoạn + runtime tôn trọng `$PORT`; một nhiệm vụ duy nhất: tạo image chạy được.
- Tạo mới: `.dockerignore` — giữ build context gọn (bin/obj/.git/node_modules/test-results).
- Sửa: `ECommerce.Web/ECommerce.Web.csproj` — thêm `Microsoft.EntityFrameworkCore.Sqlite` `10.0.12`.
- Sửa: `ECommerce.Web/Program.cs:29-31` — chọn provider (SQLite hay SQL Server).
- Sửa: `ECommerce.Web/Program.cs:92-107` — rẽ nhánh khởi tạo schema (`EnsureCreated` chỉ cho đường SQLite).
- Sửa: `ECommerce.Web/Program.cs` (cạnh `MapControllerRoute`, khoảng dòng 134) — `MapGet("/healthz")`.
- Tạo mới: `render.yaml` — Render Blueprint: web service free, health check, biến môi trường.
- Không đụng vào: `appsettings.json`, migrations trong `ECommerce.Web/Data/Migrations/`, controllers, views, tests.

---

### Task 1: Dockerfile + .dockerignore (image build được và nghe đúng $PORT)

**File:**
- Tạo mới: `Dockerfile` (thư mục gốc repo)
- Tạo mới: `.dockerignore` (thư mục gốc repo)

**Giao tiếp (Interfaces):**
- Dùng từ task trước: không có.
- Tạo ra cho task sau: image `ecommerce-demo` chạy app trên `$PORT` (mặc định `10000` nếu không có); Task 2–4 dùng image này cho mọi lần kiểm chứng.

- [x] **Bước 1: Viết probe chứng minh đang fail**

Chạy: `docker build -t ecommerce-demo .`
Kết quả mong đợi: FAIL với lỗi "ERROR: failed to solve: Dockerfile not found" (chứng minh file đang thiếu, chưa bàn nội dung).

- [x] **Bước 2: Tạo `.dockerignore` ở thư mục gốc repo**

Nội dung chính xác:

```gitignore
bin/
obj/
.git/
.vs/
node_modules/
test-results/
TestResults/
*.user
```

- [x] **Bước 3: Tạo `Dockerfile` ở thư mục gốc repo**

Nhiều giai đoạn, image và hành vi chính xác (cách làm: multi-stage chuẩn của Microsoft; ghi rõ body vì cách xử lý `$PORT` và tag preview chính là quyết định):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ECommerce.Web/ECommerce.Web.csproj ECommerce.Web/
RUN dotnet restore ECommerce.Web/ECommerce.Web.csproj
COPY . .
RUN dotnet publish ECommerce.Web/ECommerce.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet ECommerce.Web.dll"]
```

**[tự chốt]:** dùng ENTRYPOINT dạng shell chỉ để giãn nở `$PORT` (dạng exec không làm được); `PORT:-10000` khớp mặc định của Render. Không chuyển `USER app` — chỉ demo, tránh rắc rối quyền ghi file SQLite (không đặt mục tiêu: hardening container).

- [x] **Bước 4: Chạy build để xác nhận pass**

Chạy: `docker build -t ecommerce-demo .`
Kết quả mong đợi: build xong, exit 0. Nếu tag `10.0-preview` không pull được, thay cả hai tag bằng tag preview có ngày mới nhất trên `mcr.microsoft.com` (vd `10.0-preview.7`) rồi build lại — không được hạ xuống `9.0`/`8.0`.

- [x] **Bước 5: Commit**

```bash
git add Dockerfile .dockerignore
git commit -m "deploy: add Dockerfile and .dockerignore for Render free tier"
```

---

### Task 2: Chuyển provider SQLite trong Program.cs (database demo dùng-hủy)

**File:**
- Sửa: `ECommerce.Web/ECommerce.Web.csproj` (thêm package)
- Sửa: `ECommerce.Web/Program.cs:29-31` (chọn provider)
- Sửa: `ECommerce.Web/Program.cs:92-107` (rẽ nhánh khởi tạo schema)

**Giao tiếp (Interfaces):**
- Dùng từ task trước: image/mẫu lệnh `docker run` từ Task 1.
- Tạo ra cho task sau: quy tắc — connection string chứa `.db` (không phân biệt hoa thường) ⇒ `UseSqlite` + `EnsureCreated()`; ngược lại ⇒ `UseSqlServer` + `Migrate()` (hành vi dev giữ nguyên từng byte). Task 3–4 dựa vào đường SQLite này.

- [x] **Bước 1: Viết probe chứng minh đang fail**

Chạy (từ thư mục gốc repo, cần image đã build lại sau Bước 3):

```bash
docker run --rm -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/demo-probe.db" -e "SeedAdmin__Password=Demo@12345" ecommerce-demo
```

Kết quả mong đợi (trước fix): crash với lỗi `PlatformNotSupportedException` / LocalDB / lỗi kết nối SQL Server (chứng minh SQL Server vẫn đang cứng). Quan sát xong thì stop container.

- [x] **Bước 2: Thêm package SQLite trong `ECommerce.Web/ECommerce.Web.csproj`**

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
```

(đặt trong `<ItemGroup>` PackageReferences đã có).

- [x] **Bước 3: Chọn provider trong `ECommerce.Web/Program.cs:29-31`**

Thay đoạn đăng ký `AddDbContext` hiện tại bằng:

```csharp
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var useSqlite = defaultConnection.Contains(".db", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(defaultConnection);
    }
    else
    {
        options.UseSqlServer(defaultConnection);
    }
});
```

**[tự chốt]:** quy tắc substring `.db`, không thêm setting config mới — không sinh thêm cài đặt, connection string dev (`...Database=ECommerceDb;...`) không bao giờ chứa `.db`.

- [x] **Bước 4: Rẽ nhánh khởi tạo schema trong `ECommerce.Web/Program.cs:92-107`**

Trong khối `using (var scope ...)` hiện có, thay dòng `await db.Database.Migrate();` unconditional: ngay sau dòng `var db = ...GetRequiredService<ApplicationDbContext>();` chèn

```csharp
if (useSqlite)
{
    await db.EnsureCreatedAsync();
}
else
{
    await db.Database.MigrateAsync();
}
```

rồi xóa dòng `db.Database.Migrate();` cũ. Lệnh gọi `SeedAsync` bên dưới giữ nguyên.

**[tự chốt]:** dùng `EnsureCreated` (không làm bộ migration riêng cho SQLite) vì DB demo dùng-hủy; tránh phải duy trì hai lịch sử migration. Rủi ro drift đã ghi nhận: nếu entity đổi, schema SQLite demo chỉ mới khi dùng file mới.

- [x] **Bước 5: Build lại image, chạy với SQLite, kiểm chứng seed + trang chủ**

Chạy:

```bash
dotnet build ECommerce.Web.slnx --nologo --verbosity quiet
docker build -t ecommerce-demo .
docker run --rm -p 5136:10000 -e PORT=10000 -e "ConnectionStrings__DefaultConnection=Data Source=/tmp/demo.db" -e "SeedAdmin__Password=Demo@12345" ecommerce-demo
```

Kết quả mong đợi, theo thứ tự: log không báo warning mật khẩu admin mặc định (vì đã set password, nên không có warning), không exception; `curl http://localhost:5136/` trả 200 có chữ `TechNova` (đồng thời chứng minh `$PORT=10000` được tôn trọng); login tại `/Account/Login` bằng `admin@ecommerce.local` / `Demo@12345` về `/`; thêm cùng một sản phẩm vào giỏ từ hai tab trình duyệt liên tiếp → không hiện trang lỗi `database is locked`. Xong thì stop container. Chạy thêm `dotnet test ECommerce.Web.slnx` — kết quả mong đợi: 62/62 (chứng minh đường SQL Server của dev còn nguyên).

- [x] **Bước 6: Commit**

```bash
git add ECommerce.Web/ECommerce.Web.csproj ECommerce.Web/Program.cs
git commit -m "deploy: select SQLite provider for file connection strings, EnsureCreated on that path"
```

---

### Task 3: Endpoint /healthz (cho Render health check)

**File:**
- Sửa: `ECommerce.Web/Program.cs` (cạnh `MapControllerRoute`, khoảng dòng 134)

**Giao tiếp (Interfaces):**
- Dùng từ task trước: quy tắc provider từ Task 2 (health check phải trả lời được trên cả đường SQLite).
- Tạo ra cho task sau: `GET /healthz` công khai → `200 "healthy"`; `render.yaml` (Task 4) và bước warm-up demo dùng endpoint này.

- [x] **Bước 1: Viết probe chứng minh đang fail**

Chạy (container build từ Task 2): start như Task 2 Bước 5, rồi `curl -i http://localhost:5136/healthz`.
Kết quả mong đợi: `404` (chứng minh endpoint chưa có). Stop container.

- [x] **Bước 2: Thêm `MapGet("/healthz")` trong `ECommerce.Web/Program.cs`**

Đặt ngay trước `app.MapControllerRoute(...)`:

```csharp
app.MapGet("/healthz", () => Results.Ok("healthy"));
```

Không auth, không chạm DB — phải trả lời được ngay cả khi migration/seed đang chạy. **[tự chốt]:** body là chuỗi thường (không phải JSON) để người trình bày cũng curl tay được lúc demo.

- [x] **Bước 3: Build lại, xác nhận 200**

Chạy: build lại image, start container như Task 2 Bước 5, `curl -i http://localhost:5136/healthz`.
Kết quả mong đợi: `HTTP/1.1 200 OK` với body `healthy`. Stop container.

- [x] **Bước 4: Commit**

```bash
git add ECommerce.Web/Program.cs
git commit -m "deploy: add unauthenticated /healthz endpoint for host health checks"
```

---

### Task 4: render.yaml + deploy Render + kiểm chứng sau deploy (người làm thao tác dashboard)

**File:**
- Tạo mới: `render.yaml` (thư mục gốc repo)

**Giao tiếp (Interfaces):**
- Dùng từ task trước: chuẩn image (Task 1), đường SQLite (Task 2), `/healthz` (Task 3), tên biến môi trường app đã đọc (`SeedAdmin__Password`, `RateLimiting:AuthPermitLimit` — code mặc định 10, KHÔNG set trên Render).
- Tạo ra: URL demo công khai + runbook người trình bày dùng trong ngày demo.

- [x] **Bước 1: Viết probe chứng minh đang thiếu**

Chạy: `Test-Path -LiteralPath "render.yaml"`
Kết quả mong đợi: `False` (chứng minh blueprint là file mới).

- [x] **Bước 2: Tạo `render.yaml` ở thư mục gốc repo**

Nội dung chính xác (tên/giá trị đã chốt; **[tự chốt]** tên service `ecommerce-demo`):

```yaml
services:
  - type: web
    name: ecommerce-demo
    runtime: docker
    plan: free
    dockerfilePath: ./Dockerfile
    healthCheckPath: /healthz
    autoDeploy: true
    envVars:
      - key: ConnectionStrings__DefaultConnection
        value: Data Source=/tmp/ecommerce-demo.db
      - key: SeedAdmin__Password
        sync: false
```

`sync: false` buộc nhập secret trên dashboard Render (không bao giờ commit). Không set `RateLimiting__AuthPermitLimit` — traffic demo của người thật không vượt quá mặc định 10/phút.

- [x] **Bước 3: Commit + push**

```bash
git add render.yaml
git commit -m "deploy: add Render Blueprint for free Docker web service"
git push origin main
```

- [x] **Bước 4: Deploy qua dashboard (các bước làm tay, worker chỉ ghi lại — không có CLI cho việc này)**

1. Dashboard Render → New → Blueprint → chọn repo `ECommerce.Web` (nhánh `main`).
2. Khi hỏi `SeedAdmin__Password`, nhập mật khẩu mạnh (vd tự sinh, tối thiểu 8 ký tự gồm hoa/thường/số/ký tự đặc biệt theo policy Identity) rồi lưu vào trình quản lý mật khẩu — lúc demo login admin cần nó.
3. Deploy; xem log: không exception, có dòng `Now listening on`, health checker `GET /healthz 200`.
4. Mở URL công khai `https://ecommerce-demo.onrender.com` (subdomain thực tế xem trên dashboard).

- [x] **Bước 5: Kiểm chứng sau deploy (pass hết theo thứ tự)**

Chạy từ máy này:

```bash
curl -i https://<render-url>/healthz
```

Kết quả mong đợi: `200` + `healthy`. Rồi mở trình duyệt: trang chủ hiện `TechNova`; `/Products` liệt kê sản phẩm seed tiếng Việt giá `đ`; login `admin@ecommerce.local` + mật khẩu dashboard → `/Admin` hiện `Tổng quan hệ thống`; thêm sản phẩm vào giỏ dưới dạng guest → `/Cart` hiện hàng. Bước nào fail thì đọc log service Render trước (Deploy → Logs), đừng đụng code vội.

- [x] **Bước 6: Ghi lại runbook ngày demo (trả lời trong chat, không tạo thêm file)**

1. Trước 5 phút: mở URL công khai (warm-up — hấp thụ ~60s cold start sau sleep 15 phút).
2. Giữ một tab trang chủ; nếu cú click đầu treo thì refresh (đang wake sau sleep).
3. Đừng hứa dữ liệu bền: đồ tạo ra có thể mất khi restart.
4. Login fail thì khả năng instance vừa restart → login lại (key DataProtection tạm).
5. Phương án dự phòng: `dotnet run` local + share màn hình — đường LocalDB dev còn nguyên và E2E vẫn xanh.
