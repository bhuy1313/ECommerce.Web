---
name: gitnexus-area-controllers
description: "Skill for the Controllers area of ECommerce.Web. 17 symbols across 9 files."
---

# Controllers

17 symbols | 9 files | Cohesion: 80%

## When to Use

- Working with code in `ECommerce.Web/`
- Understanding how CategoryFormViewModel, CreateProduct, CreateProduct work
- Modifying controllers-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ECommerce.Web/Controllers/AdminController.cs` | CreateProduct, CreateProduct, EditProduct, EditProduct, GetCategorySelectListAsync (+3) |
| `ECommerce.Web/Controllers/AccountController.cs` | Login, Register |
| `ECommerce.Web/Models/ViewModels/CategoryFormViewModel.cs` | CategoryFormViewModel |
| `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | CategoryFormViewModel_WhenNameEmpty_FailsValidation |
| `ECommerce.Web/Services/CartService.cs` | MergeGuestCartAsync |
| `ECommerce.Web/Services/ICartService.cs` | MergeGuestCartAsync |
| `ECommerce.Web/Controllers/OrdersController.cs` | Details |
| `ECommerce.Web/Services/IOrderService.cs` | GetOrderDetailsAsync |
| `ECommerce.Web/Services/OrderService.cs` | GetOrderDetailsAsync |

## Entry Points

Start here when exploring this area:

- **`CategoryFormViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/CategoryFormViewModel.cs:4`
- **`CreateProduct`** (Method) — `ECommerce.Web/Controllers/AdminController.cs:238`
- **`CreateProduct`** (Method) — `ECommerce.Web/Controllers/AdminController.cs:248`
- **`EditProduct`** (Method) — `ECommerce.Web/Controllers/AdminController.cs:308`
- **`EditProduct`** (Method) — `ECommerce.Web/Controllers/AdminController.cs:284`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `CategoryFormViewModel` | Class | `ECommerce.Web/Models/ViewModels/CategoryFormViewModel.cs` | 4 |
| `CreateProduct` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 238 |
| `CreateProduct` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 248 |
| `EditProduct` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 308 |
| `EditProduct` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 284 |
| `CategoryFormViewModel_WhenNameEmpty_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 112 |
| `CreateCategory` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 101 |
| `EditCategory` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 137 |
| `Login` | Method | `ECommerce.Web/Controllers/AccountController.cs` | 101 |
| `Register` | Method | `ECommerce.Web/Controllers/AccountController.cs` | 41 |
| `MergeGuestCartAsync` | Method | `ECommerce.Web/Services/CartService.cs` | 337 |
| `OrderDetails` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 389 |
| `Details` | Method | `ECommerce.Web/Controllers/OrdersController.cs` | 33 |
| `GetOrderDetailsAsync` | Method | `ECommerce.Web/Services/OrderService.cs` | 113 |
| `GetCategorySelectListAsync` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 362 |
| `MergeGuestCartAsync` | Method | `ECommerce.Web/Services/ICartService.cs` | 11 |
| `GetOrderDetailsAsync` | Method | `ECommerce.Web/Services/IOrderService.cs` | 9 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `Login → Cart` | cross_community | 3 |
| `Login → CartItem` | cross_community | 3 |
| `Login → GetGuestCartItems` | cross_community | 3 |
| `Register → Cart` | cross_community | 3 |
| `Register → CartItem` | cross_community | 3 |
| `Login → DeleteGuestCartCookie` | cross_community | 3 |
| `Register → GetGuestCartItems` | cross_community | 3 |
| `Register → DeleteGuestCartCookie` | cross_community | 3 |

## How to Explore

1. `context({name: "CategoryFormViewModel"})` — see callers and callees
2. `query({search_query: "controllers"})` — find related execution flows
3. Read key files listed above for implementation details
4. `explain({target: "<file or symbol>"})` — persisted taint findings (source→sink data flows), when indexed with `--pdg`
