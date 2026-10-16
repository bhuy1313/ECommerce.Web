import { test, expect } from '@playwright/test';

test.describe('Error Handling & Friendly Error Pages (Phase 7)', () => {
  test('1. Đường dẫn không tồn tại trả về trang 404 thân thiện và có thể quay về trang chủ', async ({ page }) => {
    // Truy cập đường dẫn ngẫu nhiên không tồn tại
    const nonExistentUrl = `/page-does-not-exist-${Date.now()}`;
    const response = await page.goto(nonExistentUrl);

    // Kiểm tra HTTP Status Code là 404
    expect(response?.status()).toBe(404);

    // Kiểm tra giao diện thân thiện (copy thực từ Views/Shared/Error.cshtml + ErrorViewModel)
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Không tìm thấy trang (404)');
    await expect(page.getByText('HTTP 404')).toBeVisible();
    await expect(page.getByText(/không tồn tại, đường dẫn có thể/)).toBeVisible();

    // Kiểm tra thông tin kỹ thuật Request ID
    await expect(page.getByText('Request ID:')).toBeVisible();

    // Bấm nút "Trang chủ" trong khối nội dung chính
    await page.getByRole('main').getByRole('link', { name: 'Trang chủ' }).click();
    await expect(page).toHaveURL('/');
  });

  test('2. Giao diện trang lỗi 500 hiển thị đầy đủ thông tin hỗ trợ kỹ thuật và lối tắt', async ({ page }) => {
    const response = await page.goto('/Home/Error/500');

    // Kiểm tra HTTP status và giao diện lỗi 500
    expect(response?.status()).toBe(500);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Đã xảy ra sự cố kỹ thuật (500)');
    await expect(page.getByText('HTTP 500')).toBeVisible();
    await expect(page.getByText(/ghi lại tự động/)).toBeVisible();
    await expect(page.getByText('Request ID:')).toBeVisible();

    // Kiểm tra các nút điều hướng hỗ trợ người dùng trong phần nội dung chính
    const mainContent = page.getByRole('main');
    await expect(mainContent.getByRole('link', { name: 'Trang chủ' })).toBeVisible();
    await expect(mainContent.getByRole('link', { name: 'Sản phẩm' })).toBeVisible();
    await expect(mainContent.getByRole('link', { name: 'Giỏ hàng' })).toBeVisible();
  });

  test('3. Giao diện trang lỗi 403 hiển thị thông báo phân quyền', async ({ page }) => {
    const response = await page.goto('/Home/Error/403');

    expect(response?.status()).toBe(403);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Truy cập bị từ chối (403)');
    await expect(page.getByText('HTTP 403')).toBeVisible();
    await expect(page.getByText(/không có đủ thẩm quyền/)).toBeVisible();
  });
});
