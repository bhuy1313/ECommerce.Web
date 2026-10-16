---
name: gitnexus-area-services
description: "Skill for the Services area of ECommerce.Web. 62 symbols across 18 files."
---

# Services

62 symbols | 18 files | Cohesion: 78%

## When to Use

- Working with code in `ECommerce.Web/`
- Understanding how ApplicationUser, Cart, CartItem work
- Modifying services-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ECommerce.Web/Services/CartService.cs` | ClearCartAsync, DeleteGuestCartCookie, GetCartItemCountAsync, GetGuestCartItems, RemoveItemAsync (+4) |
| `ECommerce.Web.Tests/Services/OrderServiceTests.cs` | CreateOrderFromCartAsync_WhenCartEmpty_ReturnsFailure, CreateOrderFromCartAsync_WhenStockInsufficient_RollsBackAndFailsSafely, CreateOrderFromCartAsync_WhenStockSufficient_DeductsStockAndCalculatesTotalCorrectly, CancelOrderAsync_WhenOrderAlreadyCancelled_ReturnsFailure, CancelOrderAsync_WhenOrderPending_RefundsStockAndSetsCancelledStatus (+3) |
| `ECommerce.Web/Services/IOrderService.cs` | CreateOrderFromCartAsync, CancelOrderAsync, UpdateOrderStatusAsync, GetAllOrdersAsync, UpdatePaymentStatusAsync (+2) |
| `ECommerce.Web/Services/OrderService.cs` | CreateOrderFromCartAsync, CancelOrderAsync, UpdateOrderStatusAsync, GetAllOrdersAsync, UpdatePaymentStatusAsync (+2) |
| `ECommerce.Web.Tests/Services/CartServiceTests.cs` | RemoveItemAsync_RemovesItemFromCart, UpdateQuantityAsync_WhenZero_RemovesItemFromCart, AddItemAsync_WhenProductOutOfStock_ReturnsFailure, AddItemAsync_WhenQuantityExceedsStock_CapsAtAvailableStock, AddItemAsync_WhenQuantityNegativeOrZero_ReturnsFailure (+1) |
| `ECommerce.Web/Services/ICartService.cs` | ClearCartAsync, RemoveItemAsync, UpdateQuantityAsync, AddItemAsync, ICartService |
| `ECommerce.Web/Controllers/CartController.cs` | Clear, RemoveItem, UpdateQuantity, AddToCart |
| `ECommerce.Web/Controllers/AdminController.cs` | CreateCategory, UpdateOrderStatus, Orders, UpdatePaymentStatus |
| `ECommerce.Web/Controllers/OrdersController.cs` | Checkout, Cancel, Index |
| `ECommerce.Web/Models/Entities/ApplicationUser.cs` | ApplicationUser |

## Entry Points

Start here when exploring this area:

- **`ApplicationUser`** (Class) — `ECommerce.Web/Models/Entities/ApplicationUser.cs:4`
- **`Cart`** (Class) — `ECommerce.Web/Models/Entities/Cart.cs:2`
- **`CartItem`** (Class) — `ECommerce.Web/Models/Entities/CartItem.cs:2`
- **`Product`** (Class) — `ECommerce.Web/Models/Entities/Product.cs:2`
- **`Category`** (Class) — `ECommerce.Web/Models/Entities/Category.cs:2`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `ApplicationUser` | Class | `ECommerce.Web/Models/Entities/ApplicationUser.cs` | 4 |
| `Cart` | Class | `ECommerce.Web/Models/Entities/Cart.cs` | 2 |
| `CartItem` | Class | `ECommerce.Web/Models/Entities/CartItem.cs` | 2 |
| `Product` | Class | `ECommerce.Web/Models/Entities/Product.cs` | 2 |
| `Category` | Class | `ECommerce.Web/Models/Entities/Category.cs` | 2 |
| `Order` | Class | `ECommerce.Web/Models/Entities/Order.cs` | 4 |
| `OrderItem` | Class | `ECommerce.Web/Models/Entities/OrderItem.cs` | 2 |
| `CartService` | Class | `ECommerce.Web/Services/CartService.cs` | 9 |
| `OrderService` | Class | `ECommerce.Web/Services/OrderService.cs` | 7 |
| `ICartService` | Interface | `ECommerce.Web/Services/ICartService.cs` | 4 |
| `IOrderService` | Interface | `ECommerce.Web/Services/IOrderService.cs` | 5 |
| `RemoveItemAsync_RemovesItemFromCart` | Method | `ECommerce.Web.Tests/Services/CartServiceTests.cs` | 140 |
| `UpdateQuantityAsync_WhenZero_RemovesItemFromCart` | Method | `ECommerce.Web.Tests/Services/CartServiceTests.cs` | 114 |
| `CreateOrderFromCartAsync_WhenCartEmpty_ReturnsFailure` | Method | `ECommerce.Web.Tests/Services/OrderServiceTests.cs` | 32 |
| `CreateOrderFromCartAsync_WhenStockInsufficient_RollsBackAndFailsSafely` | Method | `ECommerce.Web.Tests/Services/OrderServiceTests.cs` | 91 |
| `CreateOrderFromCartAsync_WhenStockSufficient_DeductsStockAndCalculatesTotalCorrectly` | Method | `ECommerce.Web.Tests/Services/OrderServiceTests.cs` | 47 |
| `Checkout` | Method | `ECommerce.Web/Controllers/OrdersController.cs` | 51 |
| `SeedAsync` | Method | `ECommerce.Web/Data/DbInitializer.cs` | 9 |
| `CreateOrderFromCartAsync` | Method | `ECommerce.Web/Services/OrderService.cs` | 18 |
| `Clear` | Method | `ECommerce.Web/Controllers/CartController.cs` | 69 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `UpdateQuantity → DeleteGuestCartCookie` | intra_community | 5 |
| `UpdateQuantity → GetGuestCartItems` | intra_community | 4 |
| `RemoveItem → DeleteGuestCartCookie` | intra_community | 4 |
| `Login → Cart` | cross_community | 3 |
| `Login → CartItem` | cross_community | 3 |
| `UpdateOrderStatus → CancelOrderAsync` | cross_community | 3 |
| `AddToCart → GuestCartItemDto` | intra_community | 3 |
| `Checkout → Order` | cross_community | 3 |
| `Checkout → OrderItem` | cross_community | 3 |
| `Login → GetGuestCartItems` | cross_community | 3 |

## How to Explore

1. `context({name: "ApplicationUser"})` — see callers and callees
2. `query({search_query: "services"})` — find related execution flows
3. Read key files listed above for implementation details
4. `explain({target: "<file or symbol>"})` — persisted taint findings (source→sink data flows), when indexed with `--pdg`
