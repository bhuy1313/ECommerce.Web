# ECommerce.Web

Một ứng dụng E-Commerce được xây dựng với **ASP.NET Core MVC 10**, **Entity Framework Core 10**, **ASP.NET Core Identity** và **SQL Server/SQLite**. Dự án này được thực hiện nhằm mục đích **demo cho CV/Portfolio**, thể hiện khả năng thiết kế và triển khai một hệ thống bán hàng hoàn chỉnh và an toàn.

## Demo Live

- **URL:** [https://ecommerce-demo-9phe.onrender.com](https://ecommerce-demo-9phe.onrender.com)
- **Tài khoản Admin:**
  - **Môi trường Development (local):** `admin@ecommerce.local` / `Admin@12345` (được seed tự động khi chạy ở môi trường Development).
  - **Môi trường Production (live):** Được khởi tạo thông qua biến môi trường bí mật `SeedAdmin__Password`. Ứng dụng sẽ fail-fast khi khởi động nếu thiếu cấu hình này.

## Tính năng chính

### Phía khách hàng (Storefront)

- [x] **Trang chủ & Danh mục sản phẩm**: Hiển thị sản phẩm theo danh mục, tìm kiếm cơ bản
- [x] **Chi tiết sản phẩm**: Thông tin, hình ảnh, tồn kho, đánh giá sao
- [x] **Giỏ hàng**: Thêm, cập nhật số lượng, xóa sản phẩm khỏi giỏ hàng
- [x] **Thanh toán (Checkout)**: Nhập thông tin giao nhận, sổ địa chỉ, áp mã giảm giá, tính phí vận chuyển và thuế
- [x] **Đánh giá sản phẩm**: Chỉ khách hàng đã mua sản phẩm (verified purchase) mới có thể gửi đánh giá (chờ duyệt bởi admin)
- [x] **Tài khoản cá nhân**: Hồ sơ người dùng, quản lý sổ địa chỉ, lịch sử chi tiết đơn hàng
- [x] **Mã giảm giá (Coupon)**: Áp dụng mã giảm giá theo tỷ lệ (%) hoặc số tiền cố định tại trang checkout

### Phía quản trị (Admin Portal)

- [x] **Dashboard**: Thống kê tổng quan đơn hàng, sản phẩm, doanh thu và tài khoản
- [x] **Quản lý sản phẩm**: CRUD sản phẩm, quản lý tồn kho, upload ảnh đại diện
- [x] **Quản lý danh mục**: CRUD danh mục sản phẩm, chuyển đổi trạng thái hiển thị
- [x] **Quản lý đơn hàng**: Xem chi tiết đơn hàng, cập nhật trạng thái xử lý
- [x] **Quản lý người dùng**: Danh sách người dùng, phân quyền (Admin / Operator / Customer)
- [x] **Quản lý mã giảm giá**: CRUD coupon, thiết lập thời hạn, số lượt dùng tối đa, giá trị đơn tối thiểu
- [x] **Quản lý đánh giá**: Duyệt hoặc từ chối các đánh giá của khách hàng
- [x] **Quản lý vận chuyển & thuế**: Cấu hình các phương thức vận chuyển và thuế suất VAT

### Thanh toán & Xử lý

- [x] **COD (Cash on Delivery)**: Phương thức thanh toán khi nhận hàng, luôn sẵn sàng
- [x] **VNPay**: Tích hợp thanh toán trực tuyến qua cổng VNPay (checksum HMAC-SHA512 an toàn, Return URL và IPN callback có xác thực tính toàn vẹn và chống trùng lặp bằng idempotency). Mặc định **tắt hiển thị** trên giao diện checkout khi chưa có cấu hình credentials (`VnPay:TmnCode`/`HashSecret`/`ReturnUrl`), bản demo ưu tiên COD.
- [x] **Email thông báo**: Trong bản demo sử dụng `LogEmailSender` — nội dung email (xác thực tài khoản, reset mật khẩu, xác nhận đơn hàng) được ghi trực tiếp vào log ứng dụng (`ILogger`), không gửi qua SMTP thật ra ngoài internet.

## Công nghệ sử dụng

| Công nghệ | Mục đích |
|---|---|
| **[ASP.NET Core MVC 10](https://learn.microsoft.com/aspnet/core)** | Web framework chính (.NET 10) |
| **[Entity Framework Core 10](https://learn.microsoft.com/ef/core)** | ORM, quản lý dữ liệu, schema và migrations |
| **[ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity)** | Xác thực, phân quyền người dùng, lockout 5 lần sai / 5 phút |
| **[SQL Server / SQLite](https://learn.microsoft.com/sql)** | Cơ sở dữ liệu (LocalDB cho dev / SQLite cho container Render) |
| **[xUnit](https://xunit.net/) & [Moq](https://github.com/devlooped/moq)** | Unit testing & mocking framework |
| **[Tailwind CSS](https://tailwindcss.com/)** | Styling giao diện |
| **[Font Awesome](https://fontawesome.com/)** | Bộ biểu tượng icon |
| **[Docker](https://www.docker.com/)** | Containerize ứng dụng cho deployment |

## Kiến trúc dự án

```
ECommerce.Web/
├── Controllers/     # MVC Controllers (Account, Cart, Orders, Admin, Products, ...)
├── Data/            # ApplicationDbContext, Migrations, DbInitializer
├── Models/          # Entities, ViewModels, Enums, Configuration Options
├── Services/        # Business logic (Cart, Order, Payment, Coupon, Shipping, Email, ...)
├── Views/           # Razor Views (Storefront + Admin Layout)
├── wwwroot/         # Static files (css, js, images, uploads)
└── Program.cs       # Application configuration, DI container, security middleware
```

Dự án áp dụng các nguyên tắc:

- **Separation of Concerns**: Tách biệt rõ ràng tầng Controller, Service và Data Access
- **Dependency Injection**: Tận dụng built-in DI container của ASP.NET Core
- **Repository/Service Pattern**: Tập trung business logic và bảo vệ DbContext khỏi logic rải rác
- **Validation 2 lớp**: DataAnnotations (client + server) kèm server-side business rules
- **Security First**: AntiForgeryToken trên mọi action mutating, Rate Limiting ở production, lockout bảo vệ brute-force, role-based authorization ma trận chặt chẽ

## Giới hạn đã biết (Known Limitations)

Bản demo được thiết kế nhằm mục đích trình diễn năng lực thiết kế và lập trình cho Portfolio/CV, do đó có một số giới hạn kỹ thuật có chủ đích:
- **Lưu trữ hình ảnh**: Ảnh upload lưu trữ trên disk tạm thời của container Render free tier (sẽ bị reset về dữ liệu mặc định khi redeploy hoặc restart container).
- **Cold Start (Render free tier)**: Sau khoảng 15 phút không có lượt truy cập, server Render sẽ tự động ngủ đông. Lần truy cập đầu tiên có thể mất 30–50 giây để khởi động lại.
- **Email không gửi ra ngoài**: Hệ thống dùng `LogEmailSender` ghi thông điệp mail vào log thay vì tích hợp nhà cung cấp SMTP thật (SendGrid, Mailgun).
- **Hạ tầng bổ trợ**: Chưa tích hợp background job runner (Hangfire/Quartz) và phân tán cache (Redis) để giữ kiến trúc tối giản, nhẹ nhàng.
- **Phạm vi tính năng đã đóng băng**: Các tính năng phức tạp thuộc P2 (Wishlist, bộ lọc sản phẩm đa tiêu chí, xuất báo cáo Excel, audit log) được đóng băng có chủ đích theo [docs/scope-lock.md](docs/scope-lock.md) để tập trung bảo vệ luồng mua hàng cốt lõi (Golden Path).

## Chạy dự án local

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (hoặc SQLite)

### Cài đặt

```bash
# Clone repo
git clone https://github.com/your-username/ECommerce.Web.git
cd ECommerce.Web

# Restore dependencies
dotnet restore

# Cập nhật database (chạy migrations)
dotnet ef database update

# Chạy ứng dụng
dotnet run --project ECommerce.Web
```

Ứng dụng sẽ chạy tại `https://localhost:7133` và `http://localhost:5135`. Tài khoản admin sẽ được seed tự động với mật khẩu `Admin@12345` khi chạy ở môi trường Development.

## Kiểm thử

```bash
# Chạy toàn bộ unit test
dotnet test
```

**Kết quả hiện tại:** `277/277` tests passed (bao gồm test phân quyền, giỏ hàng, đặt hàng, mã giảm giá, kiểm tra tính toán và guards thanh toán).

## Screenshots (Gợi ý)

> Thêm screenshot của trang chủ, giỏ hàng, trang admin dashboard vào đây để portfolio thêm chuyên nghiệp.

- `screenshots/home.png`
- `screenshots/cart.png`
- `screenshots/admin-dashboard.png`
- `screenshots/checkout.png`

## Học hỏi từ dự án

Qua dự án này, tôi đã rèn luyện được:

- Thiết kế schema quan hệ và quản lý migration với Entity Framework Core 10
- Xây dựng luồng mua hàng hoàn chỉnh, an toàn giao dịch (transaction) trừ tồn kho (cart → checkout → order)
- Tích hợp cổng thanh toán bên thứ 3 (VNPay) với checksum HMAC-SHA512, callback xử lý an toàn và cơ chế idempotency
- Áp dụng phân quyền ma trận (Admin / Operator), bảo vệ chống brute-force (Identity Lockout) và Rate Limiting
- Viết unit test & integration test bao phủ toàn bộ luồng nghiệp vụ cốt lõi
- Container hóa và triển khai ứng dụng lên Render với Docker + SQLite

## License

Dự án này được thực hiện với mục đích học tập và demo portfolio.  
Nếu bạn có bất kỳ góp ý nào, hãy mở issue hoặc liên hệ với tôi!
