---
name: gitnexus-area-viewmodels
description: "Skill for the ViewModels area of ECommerce.Web. 15 symbols across 9 files."
---

# ViewModels

15 symbols | 9 files | Cohesion: 97%

## When to Use

- Working with code in `ECommerce.Web/`
- Understanding how CartItemViewModel, CartViewModel, EditUserRolesViewModel work
- Modifying viewmodels-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs` | EditUserRolesViewModel, RoleSelectionViewModel, UserListItemViewModel |
| `ECommerce.Web/Controllers/AdminController.cs` | EditUserRoles, Index, Users |
| `ECommerce.Web/Models/ViewModels/CartViewModel.cs` | CartItemViewModel, CartViewModel |
| `ECommerce.Web/Controllers/CartController.cs` | Checkout, Index |
| `ECommerce.Web/Services/CartService.cs` | GetCartAsync |
| `ECommerce.Web/Services/ICartService.cs` | GetCartAsync |
| `ECommerce.Web/Models/ViewModels/AdminDashboardViewModel.cs` | AdminDashboardViewModel |
| `ECommerce.Web/Models/ViewModels/ProductListViewModel.cs` | ProductListViewModel |
| `ECommerce.Web/Controllers/ProductsController.cs` | Index |

## Entry Points

Start here when exploring this area:

- **`CartItemViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/CartViewModel.cs:2`
- **`CartViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/CartViewModel.cs:16`
- **`EditUserRolesViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs:12`
- **`RoleSelectionViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs:19`
- **`AdminDashboardViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/AdminDashboardViewModel.cs:4`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `CartItemViewModel` | Class | `ECommerce.Web/Models/ViewModels/CartViewModel.cs` | 2 |
| `CartViewModel` | Class | `ECommerce.Web/Models/ViewModels/CartViewModel.cs` | 16 |
| `EditUserRolesViewModel` | Class | `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs` | 12 |
| `RoleSelectionViewModel` | Class | `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs` | 19 |
| `AdminDashboardViewModel` | Class | `ECommerce.Web/Models/ViewModels/AdminDashboardViewModel.cs` | 4 |
| `ProductListViewModel` | Class | `ECommerce.Web/Models/ViewModels/ProductListViewModel.cs` | 4 |
| `UserListItemViewModel` | Class | `ECommerce.Web/Models/ViewModels/UserManagementViewModel.cs` | 2 |
| `Checkout` | Method | `ECommerce.Web/Controllers/CartController.cs` | 78 |
| `Index` | Method | `ECommerce.Web/Controllers/CartController.cs` | 14 |
| `GetCartAsync` | Method | `ECommerce.Web/Services/CartService.cs` | 29 |
| `EditUserRoles` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 466 |
| `Index` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 38 |
| `Index` | Method | `ECommerce.Web/Controllers/ProductsController.cs` | 16 |
| `Users` | Method | `ECommerce.Web/Controllers/AdminController.cs` | 439 |
| `GetCartAsync` | Method | `ECommerce.Web/Services/ICartService.cs` | 6 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `Checkout → CartItemViewModel` | intra_community | 3 |
| `Checkout → CartViewModel` | intra_community | 3 |
| `Index → CartItemViewModel` | intra_community | 3 |
| `Index → CartViewModel` | intra_community | 3 |
| `Checkout → GetGuestCartItems` | cross_community | 3 |
| `Index → GetGuestCartItems` | cross_community | 3 |

## How to Explore

1. `context({name: "CartItemViewModel"})` — see callers and callees
2. `query({search_query: "viewmodels"})` — find related execution flows
3. Read key files listed above for implementation details
4. `explain({target: "<file or symbol>"})` — persisted taint findings (source→sink data flows), when indexed with `--pdg`
