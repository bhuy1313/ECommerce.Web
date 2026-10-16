import { test, expect } from '@playwright/test';

test('trang chủ tải và hiển thị tên site', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/TechNova/);
});

test('trang chủ TechNova hiển thị hero banner', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/Thế hệ thiết bị mới/);
});
