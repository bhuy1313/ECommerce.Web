---
name: gitnexus-area-models
description: "Skill for the Models area of ECommerce.Web. 11 symbols across 5 files."
---

# Models

11 symbols | 5 files | Cohesion: 91%

## When to Use

- Working with code in `ECommerce.Web.Tests/`
- Understanding how ProductFormViewModel, RegisterViewModel, ErrorViewModel work
- Modifying models-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | ProductFormViewModel_WhenNameEmpty_FailsValidation, ProductFormViewModel_WhenPriceZeroOrNegative_FailsValidation, ProductFormViewModel_WhenStockQuantityNegative_FailsValidation, ProductFormViewModel_WhenValid_PassesValidation, RegisterViewModel_WhenEmailInvalid_FailsValidation (+2) |
| `ECommerce.Web/Models/ViewModels/ProductFormViewModel.cs` | ProductFormViewModel |
| `ECommerce.Web/Models/ViewModels/RegisterViewModel.cs` | RegisterViewModel |
| `ECommerce.Web/Models/ErrorViewModel.cs` | ErrorViewModel |
| `ECommerce.Web/Controllers/HomeController.cs` | Error |

## Entry Points

Start here when exploring this area:

- **`ProductFormViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/ProductFormViewModel.cs:5`
- **`RegisterViewModel`** (Class) — `ECommerce.Web/Models/ViewModels/RegisterViewModel.cs:4`
- **`ErrorViewModel`** (Class) — `ECommerce.Web/Models/ErrorViewModel.cs:2`
- **`ProductFormViewModel_WhenNameEmpty_FailsValidation`** (Method) — `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs:55`
- **`ProductFormViewModel_WhenPriceZeroOrNegative_FailsValidation`** (Method) — `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs:74`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `ProductFormViewModel` | Class | `ECommerce.Web/Models/ViewModels/ProductFormViewModel.cs` | 5 |
| `RegisterViewModel` | Class | `ECommerce.Web/Models/ViewModels/RegisterViewModel.cs` | 4 |
| `ErrorViewModel` | Class | `ECommerce.Web/Models/ErrorViewModel.cs` | 2 |
| `ProductFormViewModel_WhenNameEmpty_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 55 |
| `ProductFormViewModel_WhenPriceZeroOrNegative_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 74 |
| `ProductFormViewModel_WhenStockQuantityNegative_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 93 |
| `ProductFormViewModel_WhenValid_PassesValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 34 |
| `RegisterViewModel_WhenEmailInvalid_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 146 |
| `RegisterViewModel_WhenPasswordsDoNotMatch_FailsValidation` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 128 |
| `Error` | Method | `ECommerce.Web/Controllers/HomeController.cs` | 39 |
| `ValidateModel` | Method | `ECommerce.Web.Tests/Models/ValidationAndCalculationsTests.cs` | 8 |

## How to Explore

1. `context({name: "ProductFormViewModel"})` — see callers and callees
2. `query({search_query: "models"})` — find related execution flows
3. Read key files listed above for implementation details
4. `explain({target: "<file or symbol>"})` — persisted taint findings (source→sink data flows), when indexed with `--pdg`
