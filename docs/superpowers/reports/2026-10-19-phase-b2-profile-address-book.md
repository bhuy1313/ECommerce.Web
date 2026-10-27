# Report — Phase B2: Hồ sơ & sổ địa chỉ

**Ngày:** 2026-10-19
**Plan:** `docs/superpowers/plans/2026-10-19-phase-b2-profile-address-book.md`
**Spec:** `docs/superpowers/specs/2026-10-19-phase-b2-profile-address-book-design.md`

## Kết quả

- Commit: `e04ac31` (entity + migration + service), `29acf47` (profile page), `c4afdc6` (checkout prefill + picker).
- Baseline trước: 159 test. Sau: **178/178 test pass** (+19).
- Build `dotnet build ECommerce.Web.slnx --no-incremental`: **0 warning, 0 error**.
- Migration `20261019014024_AddAddresses` đã áp vào dev LocalDB (bảng `Addresses`, FK cascade, index `UserId`).

## Thay đổi chính

- `Address` (UserId/RecipientName/RecipientPhone/AddressLine/IsDefault) + `ApplicationUser.Addresses`.
- `IAddressService`/`AddressService`: một mặc định/user, tự thăng cấp địa chỉ cũ nhất khi xóa mặc định, chặn id của user khác.
- `ProfileController` + `/Profile`: thêm, xóa, đặt mặc định; link trong menu tài khoản.
- Checkout: prefill từ địa chỉ mặc định ở server; dropdown chọn địa chỉ đã lưu (JS điền input); server không tin `SavedAddressId`.

## Live verify (một lần boot, teardown sạch)

User mới `b2.1792374863@technova.local`, 13/13 CHECK = True, `FAILED_CHECKS=0`:

```
register_ok, profile_menu_link, profile_page_ok, add_two_addresses,
first_address_is_default, parsed_ids, set_default_switches_order, add_to_cart,
checkout_prefilled_and_options, order_saved_address_snapshot,
delete_default_promotes_remaining, delete_last_shows_empty_state, teardown_clean
```

Port 5080 không còn listen sau khi chạy.

## Side effect dev DB

- Order `18013` (COD, note `B2 live verify`, địa chỉ `Số 2 Lê Lợi, TP.HCM`).
- Users live-verify: `b2.1792374695@`, `b2.1792374774@` (2 lần chạy lỗi giữa chừng, còn địa chỉ), `b2.1792374863@` (lần chạy thành công, đã xóa hết địa chỉ). Mật khẩu `Qa!23456`.

## Graph gate

`node .gitnexus/run.cjs detect-changes --scope compare --base-ref ac5b1ea --repo .`

- 24 files / 16 symbols / 3 execution flows, **risk medium**, không `partial`/`truncated`.
- Flows bị ảnh hưởng: cart index/view-model paths (do thay đổi `CartController` và `CheckoutViewModel`).

## Ghi chú

- Ba lần chạy live-verify vì script ban đầu dùng route `/Product` (thực tế `/Products`) và parse sản phẩm đầu tiên có thể hết hàng; lần cuối chọn sản phẩm còn tồn kho và pass toàn bộ.
- Không đổi `OrdersController`/`OrderService`; đơn hàng vẫn lưu snapshot địa chỉ.
