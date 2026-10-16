import { test, expect } from '@playwright/test';

test.describe('Authentication & Authorization Flow', () => {
  const uniqueId = Date.now();
  const testCustomerEmail = `customer_${uniqueId}@example.com`;
  const testPassword = 'Password123!';

  test('1. User có thể đăng ký tài khoản mới và tự động đăng nhập', async ({ page }) => {
    await page.goto('/Account/Register');

    // Copy thực từ Views/Account/Register.cshtml
    await expect(page.getByRole('heading', { name: 'Tạo tài khoản mới' })).toBeVisible();

    await page.fill('input[name="Email"]', testCustomerEmail);
    await page.fill('input[name="Password"]', testPassword);
    await page.fill('input[name="ConfirmPassword"]', testPassword);

    await page.locator('main button[type="submit"]').click();

    // Sau khi đăng ký thành công, tự động đăng nhập và về trang chủ.
    // _LoginPartial render email trong dropdown (hover mới thấy) nên assert attached + link Đăng nhập biến mất.
    await expect(page).toHaveURL('/');
    await expect(page.getByText(testCustomerEmail).first()).toBeAttached();
    await expect(page.getByRole('link', { name: 'Đăng nhập' })).not.toBeVisible();
  });

  test('2. Customer bị chặn khi truy cập trang Admin (/Admin -> 403 AccessDenied)', async ({ page }) => {
    // Đăng nhập bằng tài khoản Customer vừa tạo
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', testCustomerEmail);
    await page.fill('input[name="Password"]', testPassword);
    await page.locator('main button[type="submit"]').click();

    await expect(page).toHaveURL('/');

    // Cố gắng truy cập route /Admin
    await page.goto('/Admin');

    // Phải bị chuyển hướng sang AccessDenied
    await expect(page).toHaveURL(/\/Account\/AccessDenied/);
    await expect(page.getByRole('heading', { name: 'Từ chối truy cập' })).toBeVisible();
  });

  test('3. User có thể đăng xuất khỏi hệ thống', async ({ page }) => {
    // Đăng nhập
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', testCustomerEmail);
    await page.fill('input[name="Password"]', testPassword);
    await page.locator('main button[type="submit"]').click();

    // Menu tài khoản chỉ mở khi hover rồi mới bấm đăng xuất
    await page.locator('div.relative.group').hover();
    await page.getByRole('button', { name: 'Đăng xuất' }).click();

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('link', { name: 'Đăng nhập' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Đăng ký' })).toBeVisible();
  });

  test('4. Admin có thể đăng nhập và truy cập thành công Admin Dashboard', async ({ page }) => {
    await page.goto('/Account/Login');

    // Tài khoản admin đã seed trong DbInitializer
    await page.fill('input[name="Email"]', 'admin@ecommerce.local');
    await page.fill('input[name="Password"]', 'Admin@12345');
    await page.locator('main button[type="submit"]').click();

    await expect(page).toHaveURL('/');
    await expect(page.getByText('admin@ecommerce.local').first()).toBeAttached();

    // Link "Trang Quản trị" nằm trong dropdown tài khoản
    await page.locator('div.relative.group').hover();
    const adminLink = page.getByRole('link', { name: 'Trang Quản trị' });
    await expect(adminLink).toBeVisible();
    await adminLink.click();

    // Vào thành công trang /Admin
    await expect(page).toHaveURL('/Admin');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Tổng quan hệ thống');
  });
});
