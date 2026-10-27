# E-Commerce v2 Core Commerce Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bổ sung các năng lực E-commerce cốt lõi còn thiếu ở v1 — thông tin giao hàng, ảnh sản phẩm, và cổng thanh toán online — để sản phẩm bán được hàng thật, rồi mở rộng sang nhóm P1/P2.

**Architecture:** Giữ nguyên kiến trúc MVC + service layer hiện có (`Controllers → I*Service → ApplicationDbContext`). Thay đổi theo chiều dọc từng tính năng: entity + migration → ViewModel/validation → service + unit test → controller → view. Cổng thanh toán được cô lập sau `IPaymentGateway` để thêm provider (VNPay, rồi MoMo/Stripe) mà không đụng `OrderService`. Ảnh dùng `ImageUrl` dạng chuỗi (URL ngoài/tương đối) thay vì upload file, vì Render free dùng disk tạm nên file upload sẽ mất khi redeploy.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server dev / SQLite prod), ASP.NET Core Identity, xUnit + Moq + EF InMemory/Sqlite, Tailwind CDN + FontAwesome (storefront/Admin), ToastNotification (notyf), Playwright E2E.

**Spec:** `docs/roadmap.md` (Phase 10) + `docs/01-Project-Overview-E-Commerce.docx`

## Global Constraints

- Target framework: `net10.0`. Test project: `ECommerce.Web.Tests` (xUnit 2.9.3, Moq 4.20.72, EF InMemory + Sqlite 10.0.12).
- Ngôn ngữ UI: tiếng Việt, tiền tệ VND qua `<vc:money amount="..." />` / `ICurrencyService`. Không tự định dạng tiền bằng tay.
- Mọi thao tác ghi (POST) phải có `[ValidateAntiForgeryToken]` và `[EnableRateLimiting("mutating")]` như các action hiện có.
- Validation phía server bằng Data Annotations; không nới lỏng rule security hiện có (`Program.cs`).
- Mỗi thay đổi schema phải đi kèm một EF migration mới; prod chạy `MigrateAsync` (xem `Program.cs:114-137`).
- Không phá vỡ 4 test hiện có trong `OrderServiceTests` mà không cập nhật chúng trong cùng task.
- Commit nhỏ theo từng task; thông điệp theo phong cách repo (`feat:`, `fix:`, `test:`, `chore:`).
- Lệnh verify chuẩn: `dotnet build` và `dotnet test`; E2E qua `npx playwright test`.

## Review Focus

Các lớp đầu vào / chế độ lỗi mà roadmap hàm ý nhưng chưa test nào phủ — mỗi dòng được gắn test ở task sở hữu code:

1. Checkout với ModelState invalid (thiếu SĐT/địa chỉ) — phải render lại form kèm lỗi, **không** tạo đơn và **không** trừ kho.
2. Callback thanh toán gửi lại nhiều lần (VNPay retry) — chỉ được ghi nhận **một** lần, đơn không bị chuyển trạng thái hai lần.
3. Callback với chữ ký sai / số tiền không khớp đơn — phải bị từ chối, `PaymentStatus` giữ nguyên.
4. Ảnh sản phẩm rỗng/null hoặc URL hỏng — view phải hiển thị placeholder, không vỡ layout.
5. Huỷ đơn đã thanh toán — hoàn kho + chuyển `Refunded`, không để `Paid` mắc kẹt.

---

# PHASE A — P0 (bắt buộc)

## Task A1: Thêm thông tin giao hàng vào entity `Order` + migration

**Files:**
- Modify: `ECommerce.Web/Models/Entities/Order.cs`
- Create: `ECommerce.Web/Data/Migrations/<timestamp>_AddOrderShippingInfo.cs` (sinh tự động)
- Test: `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs`

**Interfaces:**
- Produces: `Order.RecipientName` (string), `Order.RecipientPhone` (string), `Order.ShippingAddress` (string), `Order.Note` (string?).

- [ ] **Step 1: Viết test failing khẳng định `Order` giữ được thông tin giao hàng**

```csharp
[Fact]
public void Order_StoresShippingInfo()
{
    var order = new Order
    {
        UserId = "u1",
        RecipientName = "Nguyen Van A",
        RecipientPhone = "0901234567",
        ShippingAddress = "12 Le Loi, Q1, TP.HCM",
        Note = "Giao sau 18h",
        TotalAmount = 100m
    };
    Assert.Equal("Nguyen Van A", order.RecipientName);
    Assert.Equal("0901234567", order.RecipientPhone);
    Assert.Equal("12 Le Loi, Q1, TP.HCM", order.ShippingAddress);
    Assert.Equal("Giao sau 18h", order.Note);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~Order_StoresShippingInfo"`
Expected: FAIL — `Order` không có property `RecipientName`.

- [ ] **Step 3: Thêm property vào `Order.cs`**

Thêm 4 property `string` (3 required mặc định `= string.Empty`, `Note` để `string?`). Giữ `TotalAmount`/`Items` nguyên vẹn.

- [ ] **Step 4: Tạo migration**

Run: `dotnet ef migrations add AddOrderShippingInfo --project ECommerce.Web`
Expected: migration sinh ra 4 cột, tất cả nullable=false trừ `Note`. **Kiểm tra** giá trị mặc định cho hàng cũ: cột NOT NULL string sẽ cần default `""` — nếu EF không sinh default, sửa migration thêm `.HasDefaultValue("")` cho 3 cột bắt buộc.

- [ ] **Step 5: Chạy test + build**

Run: `dotnet build` → OK; `dotnet test --filter "FullyQualifiedName~Order_StoresShippingInfo"` → PASS.

- [ ] **Step 6: Commit**

```bash
git add ECommerce.Web/Models/Entities/Order.cs ECommerce.Web/Data/Migrations ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs
git commit -m "feat: add shipping info fields to Order"
```

## Task A2: `ShippingDetails` + `CheckoutViewModel` + đổi chữ ký service

**Files:**
- Create: `ECommerce.Web/Models/ShippingDetails.cs`
- Create: `ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs`
- Modify: `ECommerce.Web/Services/IOrderService.cs`
- Modify: `ECommerce.Web/Services/OrderService.cs:19-119`
- Modify: `ECommerce.Web.Tests/Services/OrderServiceTests.cs` (cập nhật 4 call cũ)
- Test: `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs`, `ECommerce.Web.Tests/Services/OrderServiceTests.cs`

**Interfaces:**
- Consumes: `Order` shipping fields (Task A1).
- Produces:
  - `record/class ShippingDetails { string RecipientName; string RecipientPhone; string ShippingAddress; string? Note; }`
  - `CheckoutViewModel { string RecipientName; string RecipientPhone; string ShippingAddress; string? Note; CartViewModel Cart; }`
  - `IOrderService.CreateOrderFromCartAsync(string userId, ShippingDetails shipping)` → `Task<(bool Success, string Message, int? OrderId)>`

- [ ] **Step 1: Viết test failing — shipping được lưu vào Order khi checkout thành công**

```csharp
[Fact]
public async Task CreateOrderFromCartAsync_PersistsShippingDetails()
{
    var userId = "user-ship";
    var product = new Product { Name = "Mouse", Price = 25m, StockQuantity = 5, IsActive = true };
    _db.Products.Add(product);
    await _db.SaveChangesAsync();
    var cart = new Cart { UserId = userId };
    cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1, UnitPrice = 25m });
    _db.Carts.Add(cart);
    await _db.SaveChangesAsync();

    var shipping = new ShippingDetails
    {
        RecipientName = "Tran Thi B",
        RecipientPhone = "0912345678",
        ShippingAddress = "1 Nguyen Hue, Q1",
        Note = "Gọi trước khi giao"
    };

    var result = await _orderService.CreateOrderFromCartAsync(userId, shipping);

    Assert.True(result.Success);
    var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == result.OrderId);
    Assert.Equal("Tran Thi B", order.RecipientName);
    Assert.Equal("0912345678", order.RecipientPhone);
    Assert.Equal("1 Nguyen Hue, Q1", order.ShippingAddress);
    Assert.Equal("Gọi trước khi giao", order.Note);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~PersistsShippingDetails"`
Expected: FAIL — không tồn tại overload 2 tham số.

- [ ] **Step 3: Tạo `ShippingDetails`**

```csharp
namespace ECommerce.Web.Models;

public class ShippingDetails
{
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
}
```

- [ ] **Step 4: Tạo `CheckoutViewModel` với validation**

```csharp
public class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100)]
    [Display(Name = "Người nhận")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(15)]
    [Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
    [StringLength(300)]
    [Display(Name = "Địa chỉ giao hàng")]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public CartViewModel Cart { get; set; } = new();
}
```

Thêm test vào `ValidationAndCalculationsTests`:

```csharp
[Fact]
public void CheckoutViewModel_MissingRequiredFields_IsInvalid()
{
    var vm = new CheckoutViewModel { RecipientName = "", RecipientPhone = "", ShippingAddress = "" };
    var results = new List<ValidationResult>();
    var ok = Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
    Assert.False(ok);
    Assert.Equal(3, results.Count);
}
```

- [ ] **Step 5: Đổi chữ ký service và map shipping vào Order**

`IOrderService`: đổi `CreateOrderFromCartAsync(string userId)` → `CreateOrderFromCartAsync(string userId, ShippingDetails shipping)`.
Trong `OrderService.cs:59-66`, gán 4 field từ `shipping` khi khởi tạo `Order` (trim `RecipientName/Phone/Address`, `Note` giữ null nếu rỗng).

- [ ] **Step 6: Cập nhật 4 call cũ trong `OrderServiceTests`**

Tạo helper `private static ShippingDetails Ship() => new() { RecipientName = "Test", RecipientPhone = "0900000000", ShippingAddress = "Test Address" };` và truyền vào mọi call `CreateOrderFromCartAsync(...)`.

- [ ] **Step 7: Chạy toàn bộ test**

Run: `dotnet test`
Expected: PASS toàn bộ (test cũ + 2 test mới).

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Models ECommerce.Web/Services ECommerce.Web.Tests
git commit -m "feat: persist shipping details through order service"
```

## Task A3: Nối form checkout vào ViewModel (render lại khi lỗi, không tạo đơn)

**Files:**
- Modify: `ECommerce.Web/Controllers/CartController.cs:87-111`
- Modify: `ECommerce.Web/Controllers/OrdersController.cs:56-76`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml:57-93`
- Test: `ECommerce.Web.Tests/Controllers/AdminAuthorizationTests.cs` (bổ sung test controller mới)

**Interfaces:**
- Consumes: `CheckoutViewModel`, `CreateOrderFromCartAsync(userId, shipping)` (Task A2), `ICartService.GetCartAsync()`.
- Produces: POST `Orders/Checkout` nhận `CheckoutViewModel`; GET `Cart/Checkout` render `CheckoutPreview` với `CheckoutViewModel`.

- [ ] **Step 1: Viết test failing — checkout ModelState invalid không tạo đơn**

Assert: khi POST `Orders/Checkout` với `CheckoutViewModel` thiếu `ShippingAddress`, controller trả về view `CheckoutPreview` (không redirect tới `Details`), `ModelState.IsValid == false`, và `IOrderService.CreateOrderFromCartAsync` **không** được gọi (dùng `Mock<IOrderService>` + `Mock<ICartService>` + `Mock<INotyfService>`).

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~Checkout"`
Expected: FAIL — chữ ký action chưa khớp / chưa có nhánh invalid.

- [ ] **Step 3: GET `CartController.Checkout` trả `CheckoutViewModel`**

Bọc `cart` hiện có vào `new CheckoutViewModel { Cart = cart }` và `return View("CheckoutPreview", vm);`.

- [ ] **Step 4: POST `OrdersController.Checkout` bind `CheckoutViewModel`**

Thêm tham số `CheckoutViewModel model`; inject `ICartService` vào constructor. Nếu `!ModelState.IsValid`: nạp lại `model.Cart = await _cartService.GetCartAsync()`, `_notyf.Error("Vui lòng kiểm tra thông tin nhận hàng.")`, `return View("CheckoutPreview", model);`. Nếu hợp lệ: map sang `ShippingDetails` rồi gọi service như hiện tại.

- [ ] **Step 5: Thêm input vào `CheckoutPreview.cshtml`**

Thêm 4 trường (`RecipientName`, `RecipientPhone`, `ShippingAddress` textarea, `Note` textarea) **bên trong** form `asp-action="Checkout"` hiện có (hiện form chỉ có nút submit — các input phải nằm trong cùng `<form>`). Dùng `asp-for` + `asp-validation-for`, giữ style Tailwind hiện có. Giữ radio COD; thêm radio "Chuyển khoản/Cổng thanh toán" (sẽ kích hoạt ở Task A10).

- [ ] **Step 6: Xác nhận render lại giữ dữ liệu đã nhập**

Đảm bảo form dùng `asp-for` (không hardcode `value`) để khi invalid, giá trị cũ được giữ.

- [ ] **Step 7: Chạy test + build**

Run: `dotnet test --filter "FullyQualifiedName~Checkout"` → PASS; `dotnet build` → OK.

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Controllers ECommerce.Web/Views/Cart ECommerce.Web.Tests/Controllers
git commit -m "feat: capture shipping info on checkout with server validation"
```

## Task A4: Hiển thị thông tin giao hàng ở chi tiết đơn (customer + admin)

**Files:**
- Modify: `ECommerce.Web/Views/Orders/Details.cshtml`
- Modify: `ECommerce.Web/Views/Admin/OrderDetails.cshtml`

**Interfaces:**
- Consumes: `Order.RecipientName/RecipientPhone/ShippingAddress/Note` (Task A1).

- [ ] **Step 1: Thêm khối "Thông tin nhận hàng" vào `Orders/Details.cshtml`**

Hiển thị người nhận, SĐT, địa chỉ, ghi chú (ẩn ghi chú nếu rỗng). Dùng style card/`border` sẵn có của trang.

- [ ] **Step 2: Thêm khối tương tự vào `Admin/OrderDetails.cshtml`**

Layout admin Bootstrap-style hiện có; đặt gần phần thông tin khách hàng.

- [ ] **Step 3: Verify thủ công**

Run: `dotnet run --project ECommerce.Web` → đăng nhập, tạo đơn thử, mở `/Orders/Details/{id}` và `/Admin/OrderDetails/{id}`.
Expected: cả hai trang hiển thị đúng thông tin đã nhập. Dừng dev server sau khi xác nhận.

- [ ] **Step 4: Commit**

```bash
git add ECommerce.Web/Views/Orders ECommerce.Web/Views/Admin
git commit -m "feat: show shipping info on order detail pages"
```

## Task A5: `Product.ImageUrl` + quản lý ở Admin

**Files:**
- Modify: `ECommerce.Web/Models/Entities/Product.cs`
- Modify: `ECommerce.Web/Models/ViewModels/ProductFormViewModel.cs`
- Modify: `ECommerce.Web/Views/Admin/ProductForm.cshtml`
- Modify: `ECommerce.Web/Views/Admin/Products.cshtml`
- Create: `ECommerce.Web/wwwroot/img/product-placeholder.svg`
- Create: `ECommerce.Web/Data/Migrations/<timestamp>_AddProductImageUrl.cs`
- Test: `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs`

**Interfaces:**
- Produces: `Product.ImageUrl` (string?), `ProductFormViewModel.ImageUrl` (string?).

- [ ] **Step 1: Viết test failing cho validation `ImageUrl`**

```csharp
[Theory]
[InlineData("https://cdn.example.com/a.jpg", true)]
[InlineData("/img/local.png", true)]
[InlineData("", true)]              // optional
[InlineData("not a url", false)]
public void ProductFormViewModel_ImageUrl_ValidatesUrlOrRelativePath(string url, bool expected)
{
    var vm = new ProductFormViewModel
    {
        Name = "P", Price = 1m, StockQuantity = 1, CategoryId = 1, ImageUrl = url
    };
    var results = new List<ValidationResult>();
    var ok = Validator.TryValidateObject(vm, new ValidationContext(vm), results, validateAllProperties: true);
    Assert.Equal(expected, ok);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~ImageUrl"`
Expected: FAIL — property/annotation chưa có.

- [ ] **Step 3: Thêm `ImageUrl` + validation**

`Product.ImageUrl` là `string?`. `ProductFormViewModel.ImageUrl` thêm `[Url]` kết hợp cho phép đường dẫn tương đối: dùng `[RegularExpression(@"^(https?://\S+|/\S*)$", ErrorMessage="...")]` (không dùng `[Url]` thuần vì nó từ chối `/img/...`).

- [ ] **Step 4: Thêm input vào `ProductForm.cshtml` + cột thumbnail ở `Products.cshtml`**

Input text cho ảnh (URL). Trong bảng admin, thêm cột ảnh nhỏ; nếu rỗng dùng `/img/product-placeholder.svg`.

- [ ] **Step 5: Tạo `product-placeholder.svg`**

File SVG đơn giản (icon hộp/ảnh xám), kích thước vuông.

- [ ] **Step 6: Migration + map trong controller/service CRUD**

Run: `dotnet ef migrations add AddProductImageUrl --project ECommerce.Web`. Đảm bảo `AdminController` (hoặc service CRUD sản phẩm) copy `ImageUrl` từ VM sang entity khi create/update.

- [ ] **Step 7: Chạy test + build**

Run: `dotnet test --filter "FullyQualifiedName~ImageUrl"` → PASS; `dotnet build` → OK.

- [ ] **Step 8: Commit**

```bash
git add ECommerce.Web/Models ECommerce.Web/Views/Admin ECommerce.Web/wwwroot/img ECommerce.Web/Data/Migrations ECommerce.Web.Tests
git commit -m "feat: add product image url with admin management"
```

## Task A6: Truyền `ImageUrl` xuyên suốt cart + storefront (bỏ ảnh hardcode)

**Files:**
- Modify: `ECommerce.Web/Models/ViewModels/CartViewModel.cs` (`CartItemViewModel.ImageUrl`)
- Modify: `ECommerce.Web/Services/CartService.cs` (map `product.ImageUrl`)
- Modify: `ECommerce.Web/Views/Home/Index.cshtml` (bỏ `GetProductImage` helper)
- Modify: `ECommerce.Web/Views/Products/Index.cshtml`
- Modify: `ECommerce.Web/Views/Products/Details.cshtml`
- Modify: `ECommerce.Web/Views/Cart/Index.cshtml`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml:6-38` (xóa `@functions GetProductImage`)
- Test: `ECommerce.Web.Tests/Services/CartServiceTests.cs`

**Interfaces:**
- Consumes: `Product.ImageUrl` (Task A5).
- Produces: `CartItemViewModel.ImageUrl` (string?).

- [ ] **Step 1: Viết test failing — `GetCartAsync` surface `ImageUrl`**

```csharp
[Fact]
public async Task GetCartAsync_ExposesProductImageUrl()
{
    // add product with ImageUrl = "/img/x.png" to cart, then:
    var cart = await _cartService.GetCartAsync();
    Assert.Equal("/img/x.png", cart.Items.Single().ImageUrl);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~ExposesProductImageUrl"`
Expected: FAIL — `ImageUrl` không tồn tại trên `CartItemViewModel`.

- [ ] **Step 3: Thêm `ImageUrl` vào `CartItemViewModel` và map trong `CartService`**

Trong projection của `CartService`, gán `ImageUrl = p.ImageUrl`.

- [ ] **Step 4: Thay mọi ảnh hardcode bằng `ImageUrl ?? "/img/product-placeholder.svg"`**

Grep `images.unsplash.com` và `GetProductImage` để tìm hết chỗ cần sửa. Xóa các `@functions GetProductImage` không còn dùng.

- [ ] **Step 5: Thêm test fallback placeholder (Review Focus #4)**

Test view-model level: `ImageUrl = null` → giá trị hiển thị fallback là placeholder (nếu logic fallback nằm ở service/helper, test ở đó; nếu ở view, thêm một E2E/Playwright assertion).

- [ ] **Step 6: Chạy test + build**

Run: `dotnet test` → PASS toàn bộ; `dotnet build` → OK.

- [ ] **Step 7: Commit**

```bash
git add ECommerce.Web/Models ECommerce.Web/Services ECommerce.Web/Views ECommerce.Web.Tests
git commit -m "feat: display real product images across storefront"
```

## Task A7: Nền tảng thanh toán — enum, entity `PaymentTransaction`, abstraction

**Files:**
- Create: `ECommerce.Web/Models/Entities/Enums/PaymentMethod.cs`
- Create: `ECommerce.Web/Models/Entities/Enums/PaymentTransactionStatus.cs`
- Create: `ECommerce.Web/Models/Entities/PaymentTransaction.cs`
- Create: `ECommerce.Web/Services/Payments/IPaymentGateway.cs`
- Modify: `ECommerce.Web/Models/Entities/Order.cs` (`PaymentMethod Method`)
- Modify: `ECommerce.Web/Data/ApplicationDbContext.cs` (DbSet + index)
- Create: `ECommerce.Web/Data/Migrations/<timestamp>_AddPaymentTransaction.cs`
- Test: `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs`

**Interfaces:**
- Produces:
  - `enum PaymentMethod { Cod = 0, VnPay = 1 }`
  - `enum PaymentTransactionStatus { Pending = 0, Succeeded = 1, Failed = 2 }`
  - `PaymentTransaction { int Id; int OrderId; string Provider; string TransactionRef; decimal Amount; PaymentTransactionStatus Status; string? RawResponse; DateTime CreatedAt; DateTime UpdatedAt; }`
  - `IPaymentGateway { string Provider { get; } Task<PaymentInitResult> CreatePaymentAsync(PaymentRequest request, string returnUrl, string ipAddress); Task<PaymentCallbackResult> VerifyCallbackAsync(IQueryCollection query); }`
  - `PaymentRequest { int OrderId; decimal Amount; string OrderInfo; }`
  - `PaymentInitResult { bool Success; string? RedirectUrl; string? TransactionRef; string? Error; }`
  - `PaymentCallbackResult { bool Success; string TransactionRef; decimal Amount; bool IsValidSignature; int? OrderId; }`

- [ ] **Step 1: Viết test failing cho entity + unique index**

Assert: hai `PaymentTransaction` cùng `(Provider, TransactionRef)` khi `SaveChanges` sẽ ném `DbUpdateException` (Sqlite in-memory) — chứng minh unique index idempotency.

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~PaymentTransaction"`
Expected: FAIL — entity chưa tồn tại.

- [ ] **Step 3: Tạo enum + entity + DbSet**

`ApplicationDbContext`: thêm `DbSet<PaymentTransaction>`, `HasIndex(t => new { t.Provider, t.TransactionRef }).IsUnique()`, precision `(18,2)` cho `Amount`. `Order` thêm `PaymentMethod Method { get; set; } = PaymentMethod.Cod;`.

- [ ] **Step 4: Tạo interface + DTO trong `Services/Payments/`**

Định nghĩa đúng chữ ký ở Interfaces trên; không thêm method thừa (YAGNI).

- [ ] **Step 5: Migration**

Run: `dotnet ef migrations add AddPaymentTransaction --project ECommerce.Web`.
**Kiểm tra** cột `Order.Method` cho hàng cũ cần default `0` (Cod).

- [ ] **Step 6: Chạy test + build**

Run: `dotnet test --filter "FullyQualifiedName~PaymentTransaction"` → PASS; `dotnet build` → OK.

- [ ] **Step 7: Commit**

```bash
git add ECommerce.Web/Models ECommerce.Web/Data ECommerce.Web/Services/Payments ECommerce.Web.Tests
git commit -m "feat: add payment transaction model and gateway abstraction"
```

## Task A8: `VnPayGateway` + xác thực chữ ký + `CodPaymentGateway`

**Files:**
- Create: `ECommerce.Web/Services/Payments/CodPaymentGateway.cs`
- Create: `ECommerce.Web/Services/Payments/VnPayGateway.cs`
- Create: `ECommerce.Web/Models/Configuration/VnPayOptions.cs`
- Modify: `ECommerce.Web/Program.cs` (register + bind options)
- Modify: `ECommerce.Web/appsettings.json` (+ `appsettings.Production.json`)
- Test: `ECommerce.Web.Tests/Services/VnPayGatewayTests.cs`

**Interfaces:**
- Consumes: `IPaymentGateway` (Task A7).
- Produces: `VnPayOptions { string TmnCode; string HashSecret; string BaseUrl; string ReturnUrl; }`; hai gateway implementation.

- [ ] **Step 1: Viết test failing — chữ ký VNPay đúng/sai**

```csharp
[Theory]
[InlineData(true)]
[InlineData(false)]
public void VerifyCallback_ValidatesHmacSignature(bool tamper)
{
    var options = new VnPayOptions { HashSecret = "secret", TmnCode = "T", BaseUrl = "https://sandbox", ReturnUrl = "https://app/payment/return" };
    var gateway = new VnPayGateway(options);
    var query = BuildSignedQuery(options, tamper); // helper dựng query + secure hash
    var result = gateway.VerifyCallbackAsync(query).GetAwaiter().GetResult();
    Assert.Equal(!tamper, result.IsValidSignature);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~VnPayGatewayTests"`
Expected: FAIL — class chưa tồn tại.

- [ ] **Step 3: Implement `VnPayGateway`**

Build URL thanh toán (sort params, `vnp_SecureHash` = HMAC-SHA512 trên canonical string với `HashSecret`); `VerifyCallbackAsync` tính lại hash và so sánh constant-time. Dùng `System.Security.Cryptography.HMACSHA512`; **không** thêm package ngoài.
Ref: https://sandbox.vnpayment.vn/apis/docs/ thanh toán (dùng tài liệu VNPay, không tự bịa format).

- [ ] **Step 4: Implement `CodPaymentGateway`**

`Provider = "Cod"`; `CreatePaymentAsync` trả `Success = true`, `RedirectUrl = null` (xác nhận ngay, không redirect). `VerifyCallbackAsync` ném `NotSupportedException` (COD không có callback).

- [ ] **Step 5: Đăng ký DI + config**

`Program.cs`: `builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection("VnPay"));`, `AddSingleton<CodPaymentGateway>()`, `AddScoped<IPaymentGateway, VnPayGateway>()` (hoặc một resolver theo `PaymentMethod`). `appsettings.json` thêm section `VnPay` với placeholder; Production lấy từ env (`VnPay__HashSecret`).

- [ ] **Step 6: Chạy test + build**

Run: `dotnet test --filter "FullyQualifiedName~VnPayGatewayTests"` → PASS; `dotnet build` → OK.

- [ ] **Step 7: Commit**

```bash
git add ECommerce.Web/Services/Payments ECommerce.Web/Models/Configuration ECommerce.Web/Program.cs ECommerce.Web/appsettings.json ECommerce.Web/appsettings.Production.json ECommerce.Web.Tests
git commit -m "feat: implement VNPay and COD payment gateways"
```

## Task A9: `PaymentService` — khởi tạo, callback idempotent, chuyển `PaymentStatus`/`OrderStatus`

**Files:**
- Create: `ECommerce.Web/Services/Payments/IPaymentService.cs`
- Create: `ECommerce.Web/Services/Payments/PaymentService.cs`
- Modify: `ECommerce.Web/Program.cs` (đăng ký)
- Test: `ECommerce.Web.Tests/Services/PaymentServiceTests.cs`

**Interfaces:**
- Consumes: `IPaymentGateway`, `PaymentTransaction`, `Order`/`PaymentStatus` (A7/A8).
- Produces:
  - `IPaymentService.StartPaymentAsync(int orderId, PaymentMethod method, string ipAddress)` → `Task<PaymentInitResult>`
  - `IPaymentService.HandleCallbackAsync(string provider, IQueryCollection query)` → `Task<(bool Success, string Message, int? OrderId)>`

- [ ] **Step 1: Viết 3 test failing (bao gồm Review Focus #2, #3, #5)**

- `HandleCallback_WhenValid_SetsOrderPaidAndTransactionSucceeded`
- `HandleCallback_WhenCalledTwice_ProcessesOnlyOnce` (gọi 2 lần, assert chỉ 1 transaction Succeeded, `Order` không đổi thêm)
- `HandleCallback_WhenInvalidSignature_RejectsAndLeavesOrderPending`
- `HandleCallback_WhenAmountMismatch_Rejects` (số tiền callback ≠ `order.TotalAmount`)

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test --filter "FullyQualifiedName~PaymentServiceTests"`
Expected: FAIL — service chưa tồn tại.

- [ ] **Step 3: Implement khởi tạo + callback**

`StartPaymentAsync`: tạo `PaymentTransaction` (Pending) + gọi gateway. `HandleCallbackAsync`: verify chữ ký → nếu đã có `(Provider, TransactionRef)` ở trạng thái `Succeeded` thì trả về ngay (idempotent); nếu hợp lệ + đúng tiền → `transaction.Status = Succeeded`, `order.PaymentStatus = Paid`, `order.Status = Processing` (dùng transaction DB + `CreateExecutionStrategy` như `OrderService`).

- [ ] **Step 4: Verify hoàn kho khi huỷ đơn đã trả tiền → `Refunded`**

Mở rộng `CancelOrderAsync` (đã set `Refunded` khi `Paid` — xem `OrderService.cs:185-188`): thêm test `CancelOrderAsync_WhenOrderPaid_SetsRefunded` nếu chưa có.

- [ ] **Step 5: Chạy toàn bộ test**

Run: `dotnet test` → PASS toàn bộ.

- [ ] **Step 6: Commit**

```bash
git add ECommerce.Web/Services/Payments ECommerce.Web/Program.cs ECommerce.Web.Tests
git commit -m "feat: add idempotent payment service with order status transitions"
```

## Task A10: UI + endpoint thanh toán (chọn phương thức, return/IPN, trang kết quả)

**Files:**
- Create: `ECommerce.Web/Controllers/PaymentController.cs`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml` (radio COD/VNPay hoạt động)
- Create: `ECommerce.Web/Views/Payment/Result.cshtml`
- Modify: `ECommerce.Web/Controllers/OrdersController.cs` (checkout nhận `PaymentMethod`)
- Modify: `ECommerce.Web/Views/Orders/Details.cshtml` (nhãn phương thức + trạng thái thanh toán)

**Interfaces:**
- Consumes: `IPaymentService` (Task A9).
- Produces: GET `Payment/Return` (redirect user), POST `Payment/Ipn` (server-to-server).

- [ ] **Step 1: Cho `CheckoutViewModel` + service nhận `PaymentMethod`**

Thêm `public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cod;` vào `CheckoutViewModel`.

Mở rộng chữ ký service **tường minh** (thay cho bản 2 tham số ở Task A2):
`IOrderService.CreateOrderFromCartAsync(string userId, ShippingDetails shipping, PaymentMethod method)` → set `order.Method = method` khi tạo đơn. Cập nhật mọi call site (Task A3 controller, `OrderServiceTests` helper) trong cùng bước này để `dotnet build` xanh.

- [ ] **Step 2: `PaymentController.Return` (GET) + `Ipn` (POST)**

`Return`: gọi `HandleCallbackAsync("VnPay", Request.Query)`, redirect tới `Orders/Details` kèm notyf, hoặc `Payment/Result` khi thất bại. `Ipn`: trả JSON `{RspCode, Message}` theo spec VNPay. **Không** `[Authorize]` cho `Ipn`; `Return` có thể ẩn danh nhưng phải kiểm tra chữ ký.

- [ ] **Step 3: Bật radio VNPay ở `CheckoutPreview.cshtml`**

Bỏ `disabled`; label + `asp-for="PaymentMethod"`; JS nhỏ (hoặc Razor) để hiển thị ghi chú "Bạn sẽ được chuyển tới cổng thanh toán".

- [ ] **Step 4: Tạo `Views/Payment/Result.cshtml`**

Thông báo thành công/thất bại, link về đơn hàng.

- [ ] **Step 5: Hiển thị phương thức + trạng thái thanh toán ở `Orders/Details`**

Thêm badge `PaymentMethod` và `PaymentStatus` (đã có enum sẵn).

- [ ] **Step 6: Verify thủ công COD + build/test**

Run: `dotnet run --project ECommerce.Web` → checkout COD tạo đơn `Paid`? (COD giữ `Pending` — đúng nghiệp vụ) → xác nhận UI. Run `dotnet build` + `dotnet test`. Dừng dev server.

- [ ] **Step 7: Commit**

```bash
git add ECommerce.Web/Controllers ECommerce.Web/Views ECommerce.Web/Models
git commit -m "feat: wire online payment option into checkout and return flow"
```

---

# PHASE B — P1 (mở rộng, mỗi tính năng tách thành plan riêng khi làm)

> Mỗi mục dưới đây nên có plan chi tiết riêng theo cùng format khi bắt đầu. Liệt kê file/điểm chạm chính để ước lượng.

## Task B1: Email flows (xác thực email, quên/đặt lại mật khẩu, xác nhận đơn)
- Files: `Services/IEmailSender.cs` + `SmtpEmailSender.cs` (hoặc Resend/SendGrid), `Controllers/AccountController.cs`, `Views/Account/*`, `Program.cs` (`AddDefaultTokenProviders` đã có).
- Cần: template email, token provider sẵn có, `[AllowAnonymous] ForgotPassword/ResetPassword/ConfirmEmail`, email xác nhận đơn phát từ `PaymentService`/`OrderService` sau khi tạo đơn.
- Lưu ý prod: Render free chặn SMTP outbound trên một số gói — ưu tiên API email (Resend) qua HTTP.

## Task B2: Profile + sổ địa chỉ
- Files: `Models/Entities/Address.cs`, `ApplicationUser.Addresses`, `Controllers/AccountController.cs` (Profile), `Views/Account/Profile.cshtml`, migration.
- Checkout có thể chọn địa chỉ đã lưu thay vì nhập lại.

## Task B3: Coupon / mã giảm giá
- Files: `Models/Entities/Coupon.cs` + `Enums/DiscountType.cs`, `Order.DiscountAmount` + `CouponCode`, `Services/ICouponService.cs`, checkout UI, admin CRUD.
- Constraint: validate ở server trong transaction checkout (tránh race dùng coupon quá số lần).

## Task B4: Reviews & ratings
- Files: `Models/Entities/Review.cs`, `Services/IReviewService.cs`, `Views/Products/Details.cshtml` (form + list), admin duyệt, aggregate rating trên catalog.
- Rule: chỉ user đã có đơn `Completed` chứa sản phẩm mới được review.

## Task B5: Phí vận chuyển + thuế
- Files: `Models/Entities/ShippingMethod.cs`, `Order.ShippingFee` + `TaxAmount`, `Services/IShippingCalculator.cs`, checkout summary (bỏ hardcode "Miễn phí" ở `CheckoutPreview.cshtml:130-133`), admin cấu hình.

---

# PHASE C — P2 (tăng trưởng & vận hành)

## Task C1: Catalog nâng cao
- Files: `Services/ICatalogQueryService.cs` hoặc mở rộng `ProductsController`, `Models/ViewModels/ProductListViewModel.cs` (thêm `Sort`, `MinPrice`, `MaxPrice`, `Page`), `Views/Products/Index.cshtml`.
- Sort: mới nhất / giá tăng / giá giảm; lọc khoảng giá; phân trang (page size cố định).

## Task C2: Wishlist
- Files: `Models/Entities/WishlistItem.cs`, `Services/IWishlistService.cs`, `Controllers/WishlistController.cs`, `Views/Wishlist/Index.cshtml`, nút "Yêu thích" ở `Products/Details`.

## Task C3: Reporting/export
- Files: `Services/IReportService.cs`, `Controllers/AdminController.cs` (action mới), `Views/Admin/Reports.cshtml`, export CSV (không cần package — `System.Text` + `FileResult`).

## Task C4: Audit log
- Files: `Models/Entities/AuditLog.cs`, `Services/IAuditLogService.cs`, hook vào các action Admin thay đổi dữ liệu, `Views/Admin/AuditLogs.cshtml`.

## Task C5: Sản phẩm liên quan
- Files: `Services/IProductRecommendationService.cs` (cùng category, trừ chính nó, top N), `Views/Products/Details.cshtml`.

---

## Execution Handoff

Sau khi hoàn tất Phase A (P0), chạy `dotnet build`, `dotnet test`, và `npx playwright test` (cập nhật E2E cho flow checkout + ảnh) trước khi merge. Phase B/C mỗi tính năng nên được viết thành plan riêng theo format này rồi execute.
