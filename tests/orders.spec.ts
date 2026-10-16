import { test, expect } from '@playwright/test';

// Mã đơn hiển thị dạng #ORD-00005 (D5) trên cả trang khách hàng và admin.
const ordCode = (id: string) => `#ORD-${id.padStart(5, '0')}`;

const gotoProductDetails = async (page: import('@playwright/test').Page, name: string) => {
  await page.goto('/Products');
  await page.locator('h3', { hasText: name }).getByRole('link').first().click();
  await expect(page).toHaveURL(/\/Products\/Details\/\d+/);
};

test.describe.serial('Checkout & Orders Lifecycle Flow (Phase 5)', () => {
  let testEmail: string;
  const testPassword = 'Password123!';
  let createdOrderId: string;

  test('1. Tạo đơn hàng từ giỏ, trừ tồn kho an toàn và lưu vào lịch sử đơn', async ({ page }) => {
    testEmail = `order_user_${Date.now()}@example.com`;

    // 1. Đăng ký tài khoản khách hàng mới
    await page.goto('/Account/Register');
    await page.fill('input[name="Email"]', testEmail);
    await page.fill('input[name="Password"]', testPassword);
    await page.fill('input[name="ConfirmPassword"]', testPassword);
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');

    // 2. Chọn sản phẩm "Hub USB-C 7-trong-1" (890.000 đ)
    await gotoProductDetails(page, 'Hub USB-C 7-trong-1');

    // 3. Thêm 2 chiếc vào giỏ hàng (2 x 890.000 = 1.780.000 đ)
    await page.fill('input[name="quantity"]', '2');
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click();
    await expect(page).toHaveURL('/Cart');

    // 4. Tiến hành thanh toán sang Checkout Preview
    await page.getByRole('link', { name: 'Tiến hành thanh toán' }).click();
    await expect(page).toHaveURL('/Cart/Checkout');

    // 5. Xác nhận đặt hàng (copy thực: "Xác nhận Đặt Hàng")
    await page.getByRole('button', { name: 'Xác nhận Đặt Hàng' }).click();

    // 6. Chuyển sang trang chi tiết đơn hàng
    await expect(page).toHaveURL(/\/Orders\/Details\/(\d+)/);
    const match = page.url().match(/\/Orders\/Details\/(\d+)/);
    if (match) {
      createdOrderId = match[1];
    }

    await expect(page.getByText('Đặt hàng thành công!')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(`Đơn hàng ${ordCode(createdOrderId)}`);
    await expect(page.getByText('Chờ xử lý')).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Hub USB-C 7-trong-1' })).toBeVisible();
    await expect(page.getByRole('cell', { name: '1,780,000 đ' }).first()).toBeVisible();

    // 7. Kiểm tra giỏ hàng đã được làm trống sau khi tạo đơn
    await page.goto('/Cart');
    await expect(page.getByText('Giỏ hàng của bạn đang trống')).toBeVisible();

    // 8. Xem danh sách Lịch sử đơn hàng của tôi
    await page.goto('/Orders');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Đơn hàng của tôi');
    // Lịch sử đơn dùng card (không phải table): tổng tiền là span
    await expect(page.getByText('1,780,000 đ').first()).toBeVisible();
    await expect(page.getByText(`Mã ĐH: ${ordCode(createdOrderId)}`)).toBeVisible();
  });

  test('2. Hủy đơn hàng theo quy tắc và hoàn lại số lượng tồn kho', async ({ page }) => {
    // 1. Đăng nhập lại với tài khoản vừa tạo
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', testEmail);
    await page.fill('input[name="Password"]', testPassword);
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');

    // 2. Vào trang Đơn hàng của tôi (link card: "Xem chi tiết")
    await page.goto('/Orders');
    await page.getByRole('link', { name: 'Xem chi tiết' }).first().click();

    // 3. Thực hiện hủy đơn hàng (copy thực: "Hủy đơn hàng", có confirm dialog)
    page.on('dialog', dialog => dialog.accept());
    await page.getByRole('button', { name: 'Hủy đơn hàng' }).click();

    // 4. Kiểm tra trạng thái đã chuyển sang "Đã hủy"
    await expect(page.getByText(`Đã hủy đơn hàng #${createdOrderId} và hoàn lại số lượng tồn kho thành công.`)).toBeVisible();
    await expect(page.getByText('Đã hủy', { exact: true })).toBeVisible();
    await expect(page.getByText(/hoàn trả lại tồn kho/)).toBeVisible();

    // Nút hủy đơn không còn xuất hiện
    await expect(page.getByRole('button', { name: 'Hủy đơn hàng' })).not.toBeVisible();
  });

  test('3. Admin xem tất cả đơn hàng và cập nhật quy trình trạng thái', async ({ page }) => {
    // 1. Đăng nhập Admin
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', 'admin@ecommerce.local');
    await page.fill('input[name="Password"]', 'Admin@12345');
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');

    // 2. Vào Dashboard Admin và truy cập Đơn hàng
    await page.goto('/Admin');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Tổng quan hệ thống');
    await page.getByRole('link', { name: 'Xem tất cả' }).click();

    await expect(page).toHaveURL('/Admin/Orders');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Quản lý Đơn hàng');

    // 3. Tìm kiếm đơn hàng theo mã đơn vừa tạo
    await page.fill('input[name="search"]', `#${createdOrderId}`);
    await page.click('button:has-text("Lọc")');
    await expect(page.getByRole('cell', { name: ordCode(createdOrderId) })).toBeVisible();

    // 4. Vào chi tiết đơn hàng phía Admin
    await page.getByRole('link', { name: 'Chi tiết' }).first().click();
    await expect(page).toHaveURL(`/Admin/OrderDetails/${createdOrderId}`);

    // 5. Cập nhật thanh toán sang Paid (form Đổi trạng thái thanh toán)
    await page.locator('form[action*="UpdatePaymentStatus"] select[name="status"]').selectOption({ label: 'Paid' });
    await page.locator('form[action*="UpdatePaymentStatus"] button[type="submit"]').click();
    await expect(page.getByText(/Đã cập nhật trạng thái thanh toán/)).toBeVisible();
    await expect(page.getByText('Đã thanh toán (Paid)')).toBeVisible();
  });
});
