# Phase B2 — Hồ sơ & sổ địa chỉ (implementation plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-10-19-phase-b2-profile-address-book-design.md`
**Goal:** Người dùng lưu được nhiều địa chỉ, chọn mặc định, và checkout tự điền từ địa chỉ mặc định.
**Architecture:** Entity `Address` thuộc `ApplicationUser`; `IAddressService` bọc EF Core và kiểm tra sở hữu; `ProfileController` (`/Profile`, `[Authorize]`) cho CRUD; `CartController.Checkout` prefill + dropdown chọn địa chỉ (JS-only, server không tin id).
**Tech Stack:** ASP.NET Core MVC, EF Core 10 (dev LocalDB / test SQLite in-memory), Identity, xUnit + Moq, Tailwind CSS (CDN), notyf.

## File Map

| File | Thay đổi |
| --- | --- |
| `ECommerce.Web/Models/Entities/Address.cs` | mới — entity |
| `ECommerce.Web/Models/Entities/ApplicationUser.cs` | thêm `Addresses` |
| `ECommerce.Web/Data/ApplicationDbContext.cs` | `DbSet<Address>` + cấu hình |
| `ECommerce.Web/Data/Migrations/*_AddAddresses.*` | mới — migration |
| `ECommerce.Web/Services/IAddressService.cs`, `AddressService.cs` | mới |
| `ECommerce.Web/Program.cs` | đăng ký DI |
| `ECommerce.Web/Models/ViewModels/AddressFormViewModel.cs` | mới |
| `ECommerce.Web/Models/ViewModels/ProfileViewModel.cs` | mới |
| `ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs` | thêm `SavedAddressId`, `SavedAddresses` |
| `ECommerce.Web/Models/ViewModels/SavedAddressOption.cs` | mới |
| `ECommerce.Web/Controllers/ProfileController.cs` | mới |
| `ECommerce.Web/Views/Profile/Index.cshtml`, `_AddressForm.cshtml` | mới |
| `ECommerce.Web/Views/Shared/_LoginPartial.cshtml` | thêm link |
| `ECommerce.Web/Controllers/CartController.cs` | thêm `IAddressService`, prefill |
| `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml` | dropdown + JS |
| `ECommerce.Web.Tests/Services/AddressServiceTests.cs` | mới |
| `ECommerce.Web.Tests/Controllers/ProfileControllerTests.cs` | mới |
| `ECommerce.Web.Tests/Controllers/CartControllerCheckoutTests.cs` | mới |

**Quy tắc chung**

- TDD: test đỏ trước. RED do compile-time (file mới) thì bắt buộc **mutation check**: sửa tạm 1 dòng implementation cho test fail đúng lý do, rồi **reverse-edit** khôi phục (KHÔNG `git checkout`).
- Commit theo từng task, chỉ `git add` đường dẫn cụ thể. Không stage `docs/roadmap.md`, `opencode.json`, các plan/spec cũ, `ECommerce.Web.Tests/ECommerce.Web.Tests.csproj` (nhiễu EOL).
- Giữ build **0 warning**.
- Toast dùng `INotyfService.Success/Error`.

---

### Task 1: Address entity + migration + IAddressService

**Files:**
- Create: `ECommerce.Web/Models/Entities/Address.cs`
- Modify: `ECommerce.Web/Models/Entities/ApplicationUser.cs`, `ECommerce.Web/Data/ApplicationDbContext.cs`, `ECommerce.Web/Program.cs`
- Create: `ECommerce.Web/Data/Migrations/*_AddAddresses.cs` (generated)
- Create: `ECommerce.Web/Services/IAddressService.cs`, `ECommerce.Web/Services/AddressService.cs`
- Test: `ECommerce.Web.Tests/Services/AddressServiceTests.cs`

- [ ] **Step 1: Viết test đỏ**

Tạo `ECommerce.Web.Tests/Services/AddressServiceTests.cs`:

```csharp
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;

namespace ECommerce.Web.Tests.Services;

public class AddressServiceTests
{
    private static async Task<(ECommerce.Web.Data.ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, AddressService Service)> CreateAsync()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@test.local" });
        db.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2@test.local" });
        await db.SaveChangesAsync();
        return (db, connection, new AddressService(db));
    }

    private static Task<(bool Success, string Message)> Add(
        AddressService service, string name = "An", bool makeDefault = false) =>
        service.AddAddressAsync("u1", name, "0901234567", "1 Đường Láng, Hà Nội", makeDefault);

    // Test 1: AddressServiceTests
    // AddAddressAsync_FirstAddress_BecomesDefault
    //   Add(...) -> Success true; db.Addresses.Single(a => a.UserId == "u1").IsDefault == true

    // Test 2: AddAddressAsync_SecondAddress_NotDefault
    //   Add("An"); Add("Bình") -> "An" true, "Bình" false, Count(IsDefault) == 1.

    // Test 3: AddAddressAsync_MakeDefault_ClearsPreviousDefault
    //   Add("An"); Add("Bình", makeDefault: true)
    //   -> địa chỉ mặc định duy nhất là "Bình"; Count(IsDefault) == 1

    // Test 4: GetAddressesAsync_ReturnsOnlyOwnAddresses_DefaultFirst
    //   u1 thêm "An"(default) + "Bình"; u2 thêm "Chi"
    //   -> 2 địa chỉ, phần tử [0].RecipientName == "An", không có "Chi"

    // Test 5: SetDefaultAddressAsync_WhenOwned_SwitchesDefault
    //   Add("An"); var second = Add("Bình");
    //   id = db.Addresses.Single(a => a.RecipientName == "Bình").Id
    //   -> Success true; chỉ "Bình" IsDefault

    // Test 6: SetDefaultAddressAsync_WhenForeignOrMissing_Rejects
    //   u2 có địa chỉ id X; gọi service (user "u1") với X và với 99999
    //   -> Success false, Message "Không tìm thấy địa chỉ.", IsDefault của u2 không đổi

    // Test 7: DeleteAddressAsync_WhenDefault_PromotesOldestRemaining
    //   Add("An"); Add("Bình"); Add("Chi");
    //   xóa địa chỉ "An" (đang default) -> Success true; "Bình" IsDefault, còn 2 dòng

    // Test 8: DeleteAddressAsync_WhenForeign_KeepsRow
    //   u2 có địa chỉ; gọi delete từ "u1" -> Success false, dòng vẫn còn
}
```

Viết đầy đủ 8 test theo chú thích trên (mỗi test `[Fact]`, dùng `await using var db`/`using var` theo `TestDbContextFactory`).

- [ ] **Step 2: Chạy test, xác nhận RED**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AddressServiceTests"`
Expected: KHÔNG compile (chưa có `AddressService`). Ghi nhận RED; tiếp tục.

- [ ] **Step 3: Tạo entity + cấu hình EF**

`Address.cs`:

```csharp
namespace ECommerce.Web.Models.Entities;

public class Address
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
```

`ApplicationUser.cs`: thêm `public ICollection<Address> Addresses { get; set; } = new List<Address>();`

`ApplicationDbContext.cs`:

```csharp
public DbSet<Address> Addresses => Set<Address>();
```

trong `OnModelCreating`:

```csharp
var address = builder.Entity<Address>();
address.Property(a => a.RecipientName).HasMaxLength(100);
address.Property(a => a.RecipientPhone).HasMaxLength(15);
address.Property(a => a.AddressLine).HasMaxLength(300);
address.HasOne(a => a.User)
    .WithMany(u => u.Addresses)
    .HasForeignKey(a => a.UserId)
    .OnDelete(DeleteBehavior.Cascade);
address.HasIndex(a => a.UserId);
```

- [ ] **Step 4: Tạo migration**

Run:
`dotnet ef migrations add AddAddresses --project ECommerce.Web --startup-project ECommerce.Web --output-dir Data/Migrations`

Kiểm tra file generated: `CreateTable("Addresses")`, cột `IsDefault` NOT NULL, index `IX_Addresses_UserId`, FK cascade. KHÔNG sửa tay trừ khi có lỗi.

- [ ] **Step 5: Viết `IAddressService` + `AddressService`**

```csharp
namespace ECommerce.Web.Services;

public interface IAddressService
{
    Task<IReadOnlyList<Models.Entities.Address>> GetAddressesAsync(string userId);
    Task<Models.Entities.Address?> GetDefaultAddressAsync(string userId);
    Task<(bool Success, string Message)> AddAddressAsync(
        string userId, string recipientName, string recipientPhone, string addressLine, bool makeDefault);
    Task<(bool Success, string Message)> SetDefaultAddressAsync(string userId, int addressId);
    Task<(bool Success, string Message)> DeleteAddressAsync(string userId, int addressId);
}
```

`AddressService` (constructor `ApplicationDbContext`):

- `GetAddressesAsync`: `Where(UserId).OrderByDescending(IsDefault).ThenBy(Id).ToListAsync()`.
- `GetDefaultAddressAsync`: `FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault)`.
- `AddAddressAsync`: `var makeDefault = requested || !await _db.Addresses.AnyAsync(a => a.UserId == userId);` nếu `makeDefault` thì gọi `ClearDefaultsAsync(userId)`; add entity; `SaveChangesAsync`; trả `(true, "Đã thêm địa chỉ mới.")`.
- `SetDefaultAddressAsync`: tìm theo `Id && UserId`; null → `(false, "Không tìm thấy địa chỉ.")`; nếu đã default → trả success luôn; ngược lại `ClearDefaultsAsync`, set true, save, `(true, "Đã đặt làm địa chỉ mặc định.")`.
- `DeleteAddressAsync`: tìm theo `Id && UserId`; null → `(false, "Không tìm thấy địa chỉ.")`; ghi nhớ `wasDefault`; remove + save; nếu `wasDefault` thì lấy địa chỉ còn lại `OrderBy(Id).FirstOrDefaultAsync()`, set `IsDefault = true`, save; trả `(true, "Đã xóa địa chỉ.")`.
- `ClearDefaultsAsync`: load các dòng `UserId && IsDefault`, set false (không save — caller save).

- [ ] **Step 6: Đăng ký DI**

`Program.cs` sau `builder.Services.AddScoped<ICartService, CartService>();`:

```csharp
builder.Services.AddScoped<IAddressService, AddressService>();
```

- [ ] **Step 7: Chạy test xanh**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~AddressServiceTests"`
Expected: 8/8 pass.

- [ ] **Step 7b: Mutation check (RED là compile-time)**

Tạm sửa `AddAddressAsync`: đổi `var makeDefault = requested || !hasAny;` thành `var makeDefault = requested;`
→ chạy lại filter, xác nhận `AddAddressAsync_FirstAddress_BecomesDefault` fail.
Khôi phục bằng **reverse-edit** (không `git checkout`), chạy lại → 8/8 pass.

- [ ] **Step 8: Áp migration vào dev DB**

Run: `dotnet ef database update --project ECommerce.Web --startup-project ECommerce.Web`
Expected: `Done.` (dev LocalDB; `MigrateAsync()` ở startup cũng áp dụng khi chạy app).

- [ ] **Step 9: Build + commit**

Run: `dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning, 0 error.

```powershell
git add ECommerce.Web/Models/Entities/Address.cs ECommerce.Web/Models/Entities/ApplicationUser.cs ECommerce.Web/Data/ApplicationDbContext.cs ECommerce.Web/Data/Migrations ECommerce.Web/Services/IAddressService.cs ECommerce.Web/Services/AddressService.cs ECommerce.Web/Program.cs ECommerce.Web.Tests/Services/AddressServiceTests.cs
git commit -m "feat(profile): add address entity, migration and address service"
```

---

### Task 2: Trang hồ sơ — thêm/xóa/đặt mặc định địa chỉ

**Files:**
- Create: `ECommerce.Web/Models/ViewModels/AddressFormViewModel.cs`, `ProfileViewModel.cs`
- Create: `ECommerce.Web/Controllers/ProfileController.cs`
- Create: `ECommerce.Web/Views/Profile/Index.cshtml`, `ECommerce.Web/Views/Profile/_AddressForm.cshtml`
- Modify: `ECommerce.Web/Views/Shared/_LoginPartial.cshtml`
- Test: `ECommerce.Web.Tests/Controllers/ProfileControllerTests.cs`

- [ ] **Step 1: Viết test đỏ**

`ProfileControllerTests.cs` — harness theo mẫu `AccountEmailFlowTests.CreateController`:

```csharp
private static (ProfileController Controller, Mock<UserManager<ApplicationUser>> Users,
    Mock<IAddressService> Addresses, Mock<INotyfService> Notyf) CreateController()
{
    var store = new Mock<IUserStore<ApplicationUser>>();
    var users = new Mock<UserManager<ApplicationUser>>(
        store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    users.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("u1");
    var addresses = new Mock<IAddressService>();
    var notyf = new Mock<INotyfService>();
    var controller = new ProfileController(users.Object, addresses.Object, notyf.Object);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext(),
        RouteData = new RouteData()
    };
    return (controller, users, addresses, notyf);
}
```

Test (8):

1. `Index_ReturnsViewWithAddresses` — `GetAddressesAsync("u1")` trả 2 địa chỉ → `ViewResult.Model` là `ProfileViewModel` chứa đúng 2.
2. `Add_WhenValid_AddsAndRedirects` — model hợp lệ → service `AddAddressAsync("u1", ...)` gọi 1 lần; `RedirectToActionResult.ActionName == "Index"`; `Notyf.Success` gọi 1 lần.
3. `Add_WhenServiceRejects_ShowsErrorAndRedirects` — service trả `(false, "Không tìm thấy địa chỉ.")` → `Notyf.Error("Không tìm thấy địa chỉ.")`; vẫn redirect Index.
4. `Add_WhenModelInvalid_RerendersWithAddresses` — gọi `controller.ModelState.AddModelError("RecipientName", "required")`; service `GetAddressesAsync` trả 1 địa chỉ → `ViewResult` với `ViewName == null` (hoặc `"Index"`), model giữ `NewAddress.RecipientName`.
5. `SetDefault_WhenOwned_RedirectsAndToasts` — service success → `Notyf.Success`; redirect Index.
6. `SetDefault_WhenForeign_ShowsError` — service `(false, "Không tìm thấy địa chỉ.")` → `Notyf.Error`; redirect Index.
7. `Delete_WhenOwned_RedirectsAndToasts` + `Delete_WhenForeign_ShowsError` (gộp 2 `[Fact]`).

Run RED:
`dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~ProfileControllerTests"`

- [ ] **Step 2: View models**

`AddressFormViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace ECommerce.Web.Models.ViewModels;

public class AddressFormViewModel
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
    public string AddressLine { get; set; } = string.Empty;

    [Display(Name = "Đặt làm địa chỉ mặc định")]
    public bool MakeDefault { get; set; }
}
```

`ProfileViewModel.cs`:

```csharp
using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Models.ViewModels;

public class ProfileViewModel
{
    public IReadOnlyList<Address> Addresses { get; set; } = Array.Empty<Address>();
    public AddressFormViewModel NewAddress { get; set; } = new();
}
```

- [ ] **Step 3: Controller**

```csharp
using AspNetCoreHero.ToastNotification.Abstractions;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAddressService _addressService;
    private readonly INotyfService _notyf;

    // Index (GET), Add (POST), SetDefault (POST id), Delete (POST id)
}
```

- `Index`: load addresses của `_userManager.GetUserId(User)!`, trả `View(new ProfileViewModel { Addresses = addresses })`.
- `Add`: nếu `!ModelState.IsValid` → load addresses, `return View("Index", new ProfileViewModel { Addresses = addresses, NewAddress = model });`. Ngược lại gọi `AddAddressAsync`, `Success` → `_notyf.Success(message)` / `Error`, rồi `RedirectToAction(nameof(Index))`.
- `SetDefault(int id)` / `Delete(int id)`: gọi service, toast theo `Success`, `RedirectToAction(nameof(Index))`.
- Cả 3 POST: `[HttpPost]`, `[ValidateAntiForgeryToken]`, `[EnableRateLimiting("mutating")]`.

- [ ] **Step 4: Views**

`_AddressForm.cshtml` (`@model AddressFormViewModel`) — form `asp-controller="Profile" asp-action="Add" method="post"`, các trường `asp-for` + `asp-validation-for`, checkbox `asp-for="MakeDefault"`, nút "Lưu địa chỉ". Style theo `CheckoutPreview` (label `block font-medium ...`, input `w-full px-3.5 py-2.5 border ... rounded-xl`).

`Index.cshtml` (`@model ProfileViewModel`, `ViewData["Title"] = "Hồ sơ & địa chỉ"`) — container `max-w-3xl mx-auto py-10`, tiêu đề, danh sách địa chỉ:

```html
@foreach (var address in Model.Addresses)
{
    <div class="p-4 bg-white dark:bg-gray-900 rounded-2xl border ...">
        <div class="flex items-start justify-between gap-3">
            <div>
                <p class="font-semibold">@address.RecipientName
                    @if (address.IsDefault) { <span class="...">Mặc định</span> }
                </p>
                <p class="text-sm text-gray-500">@address.RecipientPhone</p>
                <p class="text-sm text-gray-600">@address.AddressLine</p>
            </div>
            <div class="flex gap-2">
                @if (!address.IsDefault)
                {
                    <form asp-action="SetDefault" method="post">
                        <input type="hidden" name="id" value="@address.Id" />
                        <button type="submit" class="...">Đặt mặc định</button>
                    </form>
                }
                <form asp-action="Delete" method="post" onsubmit="return confirm('Xóa địa chỉ này?');">
                    <input type="hidden" name="id" value="@address.Id" />
                    <button type="submit" class="... text-red-600">Xóa</button>
                </form>
            </div>
        </div>
    </div>
}
```

Khi danh sách rỗng hiển thị text "Bạn chưa lưu địa chỉ nào."; sau đó `<partial name="_AddressForm" model="Model.NewAddress" />`.

`_LoginPartial.cshtml`: thêm ngay sau link "Đơn hàng của tôi":

```html
<a asp-controller="Profile" asp-action="Index" class="block px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800">
    <i class="fas fa-address-book mr-2 text-gray-400"></i>Hồ sơ &amp; địa chỉ
</a>
```

- [ ] **Step 5: Chạy test xanh + build**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~ProfileControllerTests"` → pass.
Run: `dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning.

- [ ] **Step 5b: Mutation check (RED là compile-time)**

Tạm đổi `Index` trả `View(new ProfileViewModel())` (bỏ danh sách) → `Index_ReturnsViewWithAddresses` phải fail.
Reverse-edit khôi phục; chạy lại filter → pass.

- [ ] **Step 6: Commit**

```powershell
git add ECommerce.Web/Models/ViewModels/AddressFormViewModel.cs ECommerce.Web/Models/ViewModels/ProfileViewModel.cs ECommerce.Web/Controllers/ProfileController.cs ECommerce.Web/Views/Profile ECommerce.Web/Views/Shared/_LoginPartial.cshtml ECommerce.Web.Tests/Controllers/ProfileControllerTests.cs
git commit -m "feat(profile): add profile page with saved address book"
```

---

### Task 3: Checkout prefill + chọn địa chỉ đã lưu

**Files:**
- Create: `ECommerce.Web/Models/ViewModels/SavedAddressOption.cs`
- Modify: `ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs`
- Modify: `ECommerce.Web/Controllers/CartController.cs`
- Modify: `ECommerce.Web/Views/Cart/CheckoutPreview.cshtml`
- Test: `ECommerce.Web.Tests/Controllers/CartControllerCheckoutTests.cs`

- [ ] **Step 1: Viết test đỏ**

`CartControllerCheckoutTests.cs`:

```csharp
private static (CartController Controller, Mock<ICartService> Cart, Mock<IAddressService> Addresses) CreateController()
{
    var cart = new Mock<ICartService>();
    cart.Setup(c => c.GetCartAsync()).ReturnsAsync(new CartViewModel
    {
        Items = { new CartItemViewModel { ProductId = 1, ProductName = "iPhone", UnitPrice = 10_000_000m, Quantity = 1, AvailableStock = 5 } }
    });
    var addresses = new Mock<IAddressService>();
    var controller = new CartController(cart.Object, Mock.Of<INotyfService>(), addresses.Object);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Name, "buyer@test.local") },
                "Test"))
        },
        RouteData = new RouteData()
    };
    return (controller, cart, addresses);
}
```

Tests (3):

1. `Checkout_WithDefaultAddress_PrefillsShippingFields` — addresses trả 1 địa chỉ `IsDefault = true` (An/0901/Số 1) → `ViewResult.Model` là `CheckoutViewModel` với `RecipientName == "An"`, `RecipientPhone == "0901234567"`, `ShippingAddress == "Số 1"`, `SavedAddressId == address.Id`.
2. `Checkout_WithSavedAddresses_ExposesOptions` — 2 địa chỉ → `SavedAddresses.Count == 2`, option đầu có `RecipientName` đúng; view name `"CheckoutPreview"`.
3. `Checkout_WithoutAddresses_LeavesFieldsEmpty` — trả list rỗng → 3 trường `string.Empty`, `SavedAddressId == null`, `SavedAddresses` rỗng.

Run RED:
`dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj --filter "FullyQualifiedName~CartControllerCheckoutTests"`

- [ ] **Step 2: VM**

`SavedAddressOption.cs`:

```csharp
namespace ECommerce.Web.Models.ViewModels;

public sealed record SavedAddressOption(
    int Id, string Display, string RecipientName, string RecipientPhone, string AddressLine);
```

`CheckoutViewModel.cs` thêm:

```csharp
public int? SavedAddressId { get; set; }

public IReadOnlyList<SavedAddressOption> SavedAddresses { get; set; } = Array.Empty<SavedAddressOption>();
```

- [ ] **Step 3: Controller**

- Thêm field + tham số ctor `IAddressService addressService` (giữ thứ tự `cartService, notyf, addressService`).
- Trong `Checkout` GET, sau kiểm tra login:

```csharp
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var addresses = userId is null
    ? Array.Empty<Address>()
    : (await _addressService.GetAddressesAsync(userId)).ToArray();
var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault);

return View("CheckoutPreview", new CheckoutViewModel
{
    Cart = cart,
    RecipientName = defaultAddress?.RecipientName ?? string.Empty,
    RecipientPhone = defaultAddress?.RecipientPhone ?? string.Empty,
    ShippingAddress = defaultAddress?.AddressLine ?? string.Empty,
    SavedAddressId = defaultAddress?.Id,
    SavedAddresses = addresses
        .Select(a => new SavedAddressOption(a.Id, $"{a.RecipientName} — {a.AddressLine}", a.RecipientName, a.RecipientPhone, a.AddressLine))
        .ToList()
});
```

Thêm `using System.Security.Claims;` và `using ECommerce.Web.Models.Entities;`.

- [ ] **Step 4: View**

Trong `CheckoutPreview.cshtml`, chèn ngay trước khối `RecipientName`:

```html
@if (Model.SavedAddresses.Any())
{
    <div>
        <label asp-for="SavedAddressId" class="block font-medium text-gray-700 dark:text-gray-300 mb-1">Địa chỉ đã lưu</label>
        <select asp-for="SavedAddressId" id="savedAddressSelect"
                class="w-full px-3.5 py-2.5 border border-gray-200 dark:border-gray-700 rounded-xl bg-white dark:bg-gray-800 text-gray-800 dark:text-gray-200 focus:ring-primary focus:border-primary">
            <option value="">— Nhập địa chỉ mới —</option>
            @foreach (var option in Model.SavedAddresses)
            {
                <option value="@option.Id" data-name="@option.RecipientName"
                        data-phone="@option.RecipientPhone" data-address="@option.AddressLine"
                        selected="@(Model.SavedAddressId == option.Id)">@option.Display</option>
            }
        </select>
    </div>
}
```

Cuối file (trong `<script>` riêng, sau script payment hiện có):

```html
<script>
    (function () {
        var select = document.getElementById('savedAddressSelect');
        if (!select) return;
        var name = document.getElementById('RecipientName');
        var phone = document.getElementById('RecipientPhone');
        var address = document.getElementById('ShippingAddress');

        select.addEventListener('change', function () {
            var option = select.options[select.selectedIndex];
            if (!select.value || !option) return;
            name.value = option.dataset.name;
            phone.value = option.dataset.phone;
            address.value = option.dataset.address;
        });
    })();
</script>
```

- [ ] **Step 5: Test xanh + toàn bộ suite**

Run: `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj` → tất cả pass (159 + 19 = 178).
Run: `dotnet build ECommerce.Web.slnx --no-incremental` → 0 warning.

- [ ] **Step 5b: Mutation check (RED là compile-time)**

Tạm bỏ prefill (đặt `RecipientName = string.Empty`) → `Checkout_WithDefaultAddress_PrefillsShippingFields` phải fail.
Reverse-edit khôi phục; chạy lại filter → pass.

- [ ] **Step 6: Commit**

```powershell
git add ECommerce.Web/Models/ViewModels/SavedAddressOption.cs ECommerce.Web/Models/ViewModels/CheckoutViewModel.cs ECommerce.Web/Controllers/CartController.cs ECommerce.Web/Views/Cart/CheckoutPreview.cshtml ECommerce.Web.Tests/Controllers/CartControllerCheckoutTests.cs
git commit -m "feat(checkout): prefill and pick saved shipping addresses"
```

---

### Task 4: Live verify + graph gate

- [ ] **Step 1: Boot dev server (một command duy nhất, có teardown)**

Theo AGENTS.md: gộp boot → drive → kill trong một command; không pid file; `ASPNETCORE_ENVIRONMENT=Development`; log redirect; đọc log bằng `FileShare.ReadWrite` nếu cần. Port `5080`.

- [ ] **Step 2: Kịch bản kiểm tra (CHECK list)**

Đăng nhập `qa.a4@technova.local` / `Qa!23456`:

1. Menu tài khoản có link "Hồ sơ & địa chỉ" → `/Profile` 200.
2. Thêm địa chỉ A ("An", "0901234567", "Số 1 Nguyễn Trãi, Hà Nội") → hiện trong danh sách + badge "Mặc định".
3. Thêm địa chỉ B ("Bình", "0912345678", "Số 2 Lê Lợi, TP.HCM") → không mặc định.
4. Bấm "Đặt mặc định" trên B → B có badge, A mất badge.
5. Thêm sản phẩm vào giỏ → `/Cart/Checkout` → 3 trường điền sẵn B; dropdown có A và B, chọn B.
6. Đổi dropdown sang A → 3 trường đổi thành dữ liệu A.
7. Đặt hàng COD → `/Orders/Details/{id}` hiển thị đúng địa chỉ A.
8. `/Profile` xóa B (đang mặc định) → A trở thành mặc định.
9. Xóa A → danh sách rỗng, hiện "Bạn chưa lưu địa chỉ nào."
10. Teardown: kill `$server.Id`, process `ECommerce.Web`, mọi `dotnet` khác `$PID`; assert `Get-NetTCPConnection -LocalPort 5080 -State Listen` = 0.

Ghi lại id đơn hàng + user đã tạo vào report (side effect dev LocalDB).

- [ ] **Step 3: Graph change gate**

Run: `node .gitnexus/run.cjs detect-changes --scope compare --base-ref ac5b1ea --repo .`
Yêu cầu: không `partial`/`truncated`; rà các process checkout/profile bị ảnh hưởng; risk cao là bình thường (đụng checkout).

- [ ] **Step 4: Report**

Ghi `.superpowers/sdd/2026-10-19-phase-b2-profile-address-book/report-phase-b2-profile-address-book.md`: commit range, số test trước/sau, kết quả live verify từng CHECK, side effect DB, graph gate.

---

## Self-review notes

- Spec D-B2-1..8 đều có bước tương ứng (entity/migration T1; controller/view T2; checkout T3; menu T2; no package — không thêm).
- Không đụng `OrdersController`/`OrderService` → không rủi ro regression đơn hàng.
- `SavedAddressId` không được dùng ở server → không có đường tin cậy dữ liệu client.
- Snapshot địa chỉ của `Order` giữ nguyên → xóa địa chỉ không ảnh hưởng đơn cũ.
