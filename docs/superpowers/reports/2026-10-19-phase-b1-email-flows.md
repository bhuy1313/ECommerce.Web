# Phase B1 — Email flows: completion report

Date: 2026-10-19
Plan: `docs/superpowers/plans/2026-10-19-phase-b1-email-flows.md`
Spec: `docs/superpowers/specs/2026-10-19-phase-b1-email-flows-design.md`

## Commits (base `7e149a6`)

| Task | Commit | Summary |
| --- | --- | --- |
| 1 | `9011743` | `IEmailSender` + dev `LogEmailSender` + DI |
| 2 | `3406dcc` | `IdentityTokenCodec` (Base64Url) + Razor email renderer + confirm template |
| 3 | `557b71e` | `EmailService` (confirmation/reset/order) + reset/order templates + DI |
| 4 | `a3e63cf` | Register sends confirmation; `ConfirmEmail`, `ResendEmailConfirmation` |
| 5 | `3132900` | `ForgotPassword`, `ResetPassword` (+ confirmation pages) |
| 6 | `ac5b1ea` | Checkout emails the order confirmation without failing the request |

## Verification

- `dotnet build ECommerce.Web.slnx --no-incremental` → **0 Warning(s), 0 Error(s)**.
- `dotnet test ECommerce.Web.Tests/ECommerce.Web.Tests.csproj` → **159 passed** (was 130; **+29**).
  - +1 LogEmailSender, +6 IdentityTokenCodec, +1 renderer (confirm), +5 EmailService, +2 renderer (reset/order),
    +7 AccountEmailFlow (Task 4), +5 AccountEmailFlow (Task 5), +2 CheckoutControllerTests.
- Live verification (one server boot, port 5080, Development, dev log sink), `FAILED_CHECKS=0`:
  `register_new_user`, `confirm_link_logged`, `confirm_success_page`, `login_after_confirm`,
  `forgot_unknown_no_email`, `forgot_known_sends_link`, `reset_password`,
  `login_with_new_password`, `order_email_sent`, `teardown_clean` — all `True`.

## Design decisions honoured

- `SignIn.RequireConfirmedEmail` intentionally stays **false** (D-B1-1): dev seed/QA accounts keep working;
  confirmation is informational. Revisit together with real SMTP/provider rollout.
- Only `LogEmailSender` is registered (D-B1-2). Swapping to a real provider is one DI line
  (`AddSingleton<IEmailSender, X>()`).
- Email failures never fail Register/Checkout (D-B1-5): both send paths are wrapped in try/catch + `LogWarning`/`LogError`.
- Identity tokens only travel in URLs as Base64Url (D-B1-4), decoded before `ConfirmEmailAsync`/`ResetPasswordAsync`.
- No account enumeration: Forgot/Resend always render/redirect to the same confirmation and log only server-side (D-B1-7).

## Side effects on the dev database (LocalDB only)

- New users from the live probes:
  - `b1.1792371420@technova.local` (first run; aborted before checkout)
  - `b1.1792371584@technova.local` (successful run; password was reset to `NewQa!23456`)
- One COD order created by the probe: **`17013`** (status/stock changed as a normal order).

## Rulings / deviations recorded in the ledger

1. Renderer test host must register the Diagnostics services a real host provides
   (`DiagnosticSource` + `DiagnosticListener` for `RazorViewEngine`/`RazorPageActivator`).
2. Email assertions compare `HtmlDecode(html)` — Razor's default `HtmlEncoder` emits numeric entities
   for non-ASCII model values.
3. Order-total assertion derives `N0` from the running culture instead of a hardcoded grouped literal.
4. Controller tests set only `HttpContext` + `RouteData` on `ControllerContext` (ActionDescriptor is typed
   `ControllerActionDescriptor`; the mocked `IUrlHelper` ignores it).
5. Task 6 RED evidence is a mutation check (hook replaced with `Task.CompletedTask`): the new test failed
   under mutation and passed after restore.
6. Live script: reading the redirected dev log while the app runs requires `FileShare.ReadWrite`
   (`ReadAllText` throws); order-email assertions match ASCII anchors (`#<id>`, `/Orders/Details/<id>`)
   because the console-logged Vietnamese subject is mojibake under the redirected console code page.

## Residuals (out of scope for B1)

- No real SMTP/provider, no HTML/text multipart, no bounce handling, no email queue/retry.
- Email body is logged to the dev console; it is not persisted anywhere.
- No "resend" throttling beyond the shared `auth` rate-limit policy (10 req/min/IP).
- `RequireConfirmedEmail` remains false; flipping it needs seed/QA account confirmations first.
