# B1 — Email flows (dev-level infrastructure) — Design

**Status:** approved in conversation, 2026-10-19
**Phase:** B1 of the E-Commerce v2 plan (`docs/superpowers/plans/2026-10-18-ecommerce-v2-core-commerce.md`, PHASE B)

## Context

`Program.cs:63-76` configures Identity with `AddDefaultTokenProviders()` and
`RequireUniqueEmail`, but no `RequireConfirmedEmail`. `AccountController` has only
Register/Login/Logout/AccessDenied. The repo has no email abstraction, no SMTP/API
client and no email templates. `Views/Account/` holds only AccessDenied, Login and
Register.

## Goal

Three flows work end-to-end in Development against an email sink that logs the
rendered message (including a clickable link), so the flow can be exercised and
verified without a real provider. A production provider later plugs in by
implementing one interface.

Flows:

1. Email confirmation — Register sends a link; `ConfirmEmail` marks the address
   confirmed; the Login page links to a resend form.
2. Forgot / reset password — request a link, open it, set a new password, log in.
3. Order confirmation — every successfully created order (COD and VNPay) sends one
   email to the buyer with items, total, shipping address and an order link.

## Non-goals

- No real provider (Resend/SendGrid/SMTP) in this change, and no `Email:Provider`
  config switch yet — swapping the one DI registration is the extension point.
- Email verification does **not** gate login.
- No plain-text alternative, localization, attachments, retry queue or background
  dispatch.
- No payment-received / refund / shipping emails.

## Decisions

| # | Decision | Why |
|---|----------|-----|
| D-B1-1 | `SignIn.RequireConfirmedEmail` stays **false** | Existing seeded/QA accounts have `EmailConfirmed = false`; gating would lock them out and add friction at purchase time. |
| D-B1-2 | Only `LogEmailSender` is registered | User asked for dev infrastructure + templates now, provider later. One-line DI swap later; no speculative config. |
| D-B1-3 | Templates are Razor views in `Views/Emails/`, rendered to a string by the app's Razor engine | Editable like any view, no new NuGet package, compiled at build. |
| D-B1-4 | Identity tokens cross the URL boundary only as Base64Url (`IdentityTokenCodec`) | Raw tokens contain `+`, `/`, `=`; `+` becomes a space in query strings and silently breaks confirmation/reset. This is the official Identity scaffold pattern. |
| D-B1-5 | Email send failures never fail Register or Checkout | The account/order already exists; the user must not see a 500 or lose the order. Failure is logged. |
| D-B1-6 | Controllers build URLs with `Url.Action(..., Request.Scheme)` | Routing stays in the controller; services stay routing-agnostic and unit-testable. |
| D-B1-7 | No account enumeration: ForgotPassword always shows the same confirmation page; Resend always reports the same conditional message | Matches the existing Register/Login anti-enumeration comments. |
| D-B1-8 | Reset does not require a confirmed email and does not auto-login | Standard Identity behavior; forcing confirmation here would block legitimate resets. |

## Architecture

```
Services/Email/
  IEmailSender.cs          SendAsync(to, subject, htmlBody, ct)
  LogEmailSender.cs        dev sink: logs "=== EMAIL (dev) ===", To, Subject, Body
  IEmailTemplateRenderer.cs RenderAsync<TModel>(viewPath, model) -> string
  RazorEmailTemplateRenderer.cs  IRazorViewEngine + ITempDataProvider + IHttpContextAccessor + IModelMetadataProvider
  IdentityTokenCodec.cs    Encode(token) / TryDecode(code, out token) via WebEncoders.Base64Url*
  IEmailService.cs         SendEmailConfirmationAsync(to, url)
                           SendPasswordResetAsync(to, url)
                           SendOrderConfirmationAsync(orderId, orderUrl)
  EmailService.cs          render template + send; loads Order for the order email
```

Registration in `Program.cs` (after `AddControllersWithViews` and
`AddHttpContextAccessor`):

```csharp
builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
builder.Services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
builder.Services.AddScoped<IEmailService, EmailService>();
```

Templates and view models:

| Template | Model | Required content |
|----------|-------|------------------|
| `Views/Emails/ConfirmEmail.cshtml` | `EmailLinkViewModel(Url)` | greeting, explanation, link, one-day expiry note |
| `Views/Emails/ResetPassword.cshtml` | `EmailLinkViewModel(Url)` | greeting, explanation, link, ignore-if-not-you note |
| `Views/Emails/OrderConfirmation.cshtml` | `OrderConfirmationEmailViewModel(Order, OrderUrl)` | order id, date, item lines (name × qty, unit price), total `N0 ₫`, recipient/phone/address, payment method text, order link |

Subjects (exact): `Xác thực email của bạn – TechNova`,
`Đặt lại mật khẩu – TechNova`, `Xác nhận đơn hàng #{id} – TechNova`.

## Flows

**Register (POST).** After `CreateAsync` + `AddToRoleAsync`, generate the
confirmation token, `IdentityTokenCodec.Encode`, build the absolute link, and
`await _emailService.SendEmailConfirmationAsync(...)` inside `try/catch` (log
warning on failure). Then sign in, merge the guest cart, redirect as today.

**ConfirmEmail (GET `userId`, `code`).** `FindByIdAsync`; `TryDecode`; reject
controls with `"Liên kết xác thực không hợp lệ hoặc đã hết hạn."`;
`ConfirmEmailAsync`; success message `"Email của bạn đã được xác thực thành công."`.
Malformed/expired tokens render the error message, never a 500.

**Resend (GET form, POST).** POST: policy `auth` + antiforgery; if the user exists
and is not confirmed, generate + send; always redirect to Login with the same
notyf information message (`Nếu email tồn tại và chưa xác thực, ...`).

**ForgotPassword (GET/POST) → ResetPassword (GET/POST).** POST always redirects to
a static confirmation page; the email is sent only when the user exists. Link
carries `userId` + Base64Url code. POST decodes the code, calls
`ResetPasswordAsync`; success redirects to a static confirmation page; failure
adds the friendly ModelState error and re-renders. Both POSTs: `auth` policy +
antiforgery.

**Order confirmation.** In `OrdersController.Checkout`, immediately after
`CreateOrderFromCartAsync` succeeds and **before** payment initialization (so it
covers COD and the VNPay redirect): build the order URL, call
`SendOrderConfirmationAsync(orderId, orderUrl)` in `try/catch` + `LogError`.
`EmailService` loads the order with `Items` + `User`; missing order or user email
logs a warning and returns without sending.

## Security and failure rules

- Every POST: `[ValidateAntiForgeryToken]` + `[EnableRateLimiting("auth")]`.
- Tokens never appear in URLs raw (D-B1-4); expired/tampered codes produce a
  friendly message.
- The dev sink intentionally logs the full message and link. This sink is for
  Development only; the future production sender must not log bodies.
- Email failure is isolated from the business transaction (D-B1-5).

## Testing

Unit (xUnit + Moq + real SQLite `TestDbContextFactory`):

- `IdentityTokenCodec`: round-trip for a token containing `+ / =`; `TryDecode`
  returns false for garbage.
- `LogEmailSender`: logs the marker, recipient, subject and body.
- `RazorEmailTemplateRenderer`: renders all three templates with real Razor
  services; output contains the URL / order id / product name.
- `EmailService`: correct recipient, subject and template path per flow; order
  email loads items; missing order / missing email → no send, no throw.
- `AccountController`: Register sends one mail and still redirects when the sender
  throws; ConfirmEmail valid/invalid/rejected token; ForgotPassword identical
  response for known/unknown with send only for known; ResetPassword success and
  garbage code.
- `OrdersController`: checkout sends once after order creation; checkout still
  redirects to Details when the email service throws.

Live verification (single script, one dev server boot): register → extract
confirm link from the log → confirm → login; forgot → extract reset link → reset
→ login with the new password; place a COD order → log contains the order email
with the order id and product name. Zero listeners / zero app processes after
teardown.

## Residuals

- Dev sink only; production requires an `IEmailSender` implementation plus a
  verified sending domain.
- Resend/forgot requests are throttled only by the shared 10/min `auth` policy.
- Token lifespan remains Identity's default (1 day).
- An already-confirmed address still reports success when its confirmation token
  is replayed (Identity semantics).
