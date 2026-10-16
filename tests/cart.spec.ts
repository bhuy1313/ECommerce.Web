import { test, expect } from '@playwright/test';

// Card sản phẩm TechNova không có class .card — vào chi tiết qua link trong h3 tên sản phẩm.
const gotoProductDetails = async (page: import('@playwright/test').Page, name: string) => {
  await page.goto('/Products');
  await page.locator('h3', { hasText: name }).getByRole('link').click();
  await expect(page).toHaveURL(/\/Products\/Details\/\d+/);
};

test.describe('Shopping Cart Flow (Phase 4)', () => {
  test('1. Guest có thể thêm sản phẩm vào giỏ, cập nhật số lượng và xóa item', async ({ page }) => {
    // 1. Vào trang chi tiết sản phẩm "Chuột không dây Silent" (549.000 đ, stock 60)
    await gotoProductDetails(page, 'Chuột không dây Silent');

    // 2. Chọn số lượng = 2 và thêm vào giỏ hàng
    await page.fill('input[name="quantity"]', '2');
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click();

    // 3. Chuyển hướng sang giỏ hàng /Cart (2 x 549.000 = 1.098.000 đ)
    await expect(page).toHaveURL('/Cart');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Giỏ hàng của bạn');
    await expect(page.getByRole('link', { name: 'Chuột không dây Silent' })).toBeVisible();
    await expect(page.getByRole('cell', { name: '1,098,000 đ' })).toBeVisible();

    // 4. Tăng số lượng lên 3 bằng nút "Tăng 1" (3 x 549.000 = 1.647.000 đ)
    await page.locator('button[title="Tăng 1"]').click();

    await expect(page.getByText('Đã cập nhật số lượng thành công.')).toBeVisible();
    await expect(page.getByRole('cell', { name: '1,647,000 đ' })).toBeVisible();

    // 5. Xóa sản phẩm khỏi giỏ hàng
    await page.locator('button[title="Xóa khỏi giỏ"]').click();
    await expect(page.getByText('Đã xóa sản phẩm khỏi giỏ hàng.')).toBeVisible();
    await expect(page.getByText('Giỏ hàng của bạn đang trống')).toBeVisible();
  });

  test('2. Kiểm tra tồn kho trước khi đặt hàng (Stock Safeguard)', async ({ page }) => {
    await gotoProductDetails(page, 'Áo khoác denim');

    // Áo khoác denim có stock = 9. Thử thêm 99 sản phẩm vượt stock.
    // Input có max=9 (HTML5 validation) nên gỡ max trước để kiểm tra safeguard phía server.
    const qtyInput = page.locator('#quantity-input');
    await qtyInput.evaluate(el => el.removeAttribute('max'));
    await qtyInput.fill('99');
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click();

    await expect(page).toHaveURL('/Cart');
    // Hệ thống tự động giới hạn ở mức 9 theo tồn kho khả dụng
    await expect(page.getByText(/giới hạn tồn kho/i)).toBeVisible();
    // Số lượng bị giới hạn về đúng 9: thành tiền 9 x 1.199.000 = 10.791.000 đ
    await expect(page.getByRole('cell', { name: '10,791,000 đ' }).first()).toBeVisible();

    // Nút "Tiến hành thanh toán" sẵn sàng vì số lượng đã được tự điều chỉnh hợp lệ
    await expect(page.getByRole('link', { name: 'Tiến hành thanh toán' })).toBeVisible();
  });

  test('3. Merge giỏ hàng từ Guest sang Logged-in khi đăng nhập', async ({ page }) => {
    // 1. Guest thêm sản phẩm "Clean Code (Tái bản)" vào giỏ hàng (dùng tên đầy đủ
    // để tránh trùng với sản phẩm legacy "Clean Code" còn sót trong DB)
    await gotoProductDetails(page, 'Clean Code (Tái bản)');

    await page.fill('input[name="quantity"]', '1');
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click();
    await expect(page).toHaveURL('/Cart');
    await expect(page.getByRole('link', { name: 'Clean Code (Tái bản)' })).toBeVisible();

    // 2. Tạo tài khoản khách hàng mới và đăng nhập
    const uniqueEmail = `buyer_${Date.now()}@test.local`;
    await page.goto('/Account/Register');
    await page.fill('input[name="Email"]', uniqueEmail);
    await page.fill('input[name="Password"]', 'BuyerPass123!');
    await page.fill('input[name="ConfirmPassword"]', 'BuyerPass123!');
    await page.locator('main button[type="submit"]').click();

    await expect(page).toHaveURL('/');

    // 3. Vào lại giỏ hàng /Cart: sản phẩm từ guest đã được chuyển sang user đã đăng nhập
    await page.goto('/Cart');
    await expect(page.getByRole('link', { name: 'Clean Code (Tái bản)' })).toBeVisible();

    // 4. Bấm "Tiến hành thanh toán" -> chuyển sang màn hình Checkout Preview thành công
    await page.getByRole('link', { name: 'Tiến hành thanh toán' }).click();
    await expect(page).toHaveURL('/Cart/Checkout');
    await expect(page.getByRole('heading', { name: 'Xác nhận & Hoàn tất đơn hàng' })).toBeVisible();
    await expect(page.getByText(/Bảo đảm tồn kho an toàn/)).toBeVisible();
  });
});
