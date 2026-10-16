import { test, expect } from '@playwright/test';

test.describe.serial('Admin Dashboard & User Management Flow (Phase 6)', () => {
  const adminEmail = 'admin@ecommerce.local';
  const adminPassword = 'Admin@12345';
  let memberEmail: string;
  const memberPassword = 'Password123!';

  const adminLogin = async (page: import('@playwright/test').Page) => {
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', adminEmail);
    await page.fill('input[name="Password"]', adminPassword);
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');
  };

  test('1. Hiển thị Admin Dashboard: KPI cards, Đơn gần đây, Cảnh báo kho và Lối tắt quản lý', async ({ page }) => {
    // Đăng nhập Admin
    await adminLogin(page);

    // Vào Admin Dashboard (h1 thực: "Tổng quan hệ thống")
    await page.goto('/Admin');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Tổng quan hệ thống');

    // Kiểm tra các KPI Cards (copy thực)
    await expect(page.getByText('Tổng doanh thu')).toBeVisible();
    await expect(page.getByText('Tổng đơn hàng')).toBeVisible();
    await expect(page.getByText('Sản phẩm & Danh mục')).toBeVisible();
    await expect(page.getByText('Khách hàng & Cảnh báo')).toBeVisible();

    // Kiểm tra khối Đơn hàng gần đây
    await expect(page.getByRole('heading', { name: /Đơn hàng phát sinh gần đây/ })).toBeVisible();

    // Kiểm tra khối Cảnh báo tồn kho ít
    await expect(page.getByRole('heading', { name: /Cảnh báo kho/ })).toBeVisible();

    // Kiểm tra 4 lối tắt quản trị (dạng link, không phải heading)
    await expect(page.getByRole('link', { name: /Thêm sản phẩm mới/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Tạo danh mục mới/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Xử lý toàn bộ đơn/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Phân quyền tài khoản/ })).toBeVisible();
  });

  test('2. Đăng ký người dùng mới và phân quyền StoreOperator từ Admin Dashboard', async ({ page }) => {
    memberEmail = `staff_member_${Date.now()}@example.com`;

    // 1. Tạo tài khoản khách hàng mới
    await page.goto('/Account/Register');
    await page.fill('input[name="Email"]', memberEmail);
    await page.fill('input[name="Password"]', memberPassword);
    await page.fill('input[name="ConfirmPassword"]', memberPassword);
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');

    // 2. Đăng xuất tài khoản mới (menu hover mới hiện nút Đăng xuất)
    await page.locator('div.relative.group').hover();
    await page.getByRole('button', { name: 'Đăng xuất' }).click();
    await expect(page).toHaveURL('/');

    // 3. Đăng nhập với Admin
    await adminLogin(page);

    // 4. Vào trang Quản lý Người dùng (h1 thực: "Quản lý Người dùng")
    await page.goto('/Admin/Users');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Quản lý Người dùng');
    await expect(page.getByText(memberEmail)).toBeVisible();

    // 5. Tìm dòng của user mới và click Phân vai trò
    const userRow = page.locator('tr', { hasText: memberEmail });
    await userRow.getByRole('link', { name: 'Phân vai trò' }).click();
    await expect(page).toHaveURL(/\/Admin\/EditUserRoles/);

    // 6. Tích chọn StoreOperator và lưu
    await page.check('input#role_StoreOperator');
    await page.getByRole('button', { name: 'Lưu thay đổi phân quyền' }).click();

    // 7. Chuyển về trang danh sách và kiểm tra badge
    await expect(page).toHaveURL('/Admin/Users');
    await expect(page.getByText(`Đã cập nhật vai trò phân quyền cho tài khoản '${memberEmail}' thành công.`)).toBeVisible();

    const updatedUserRow = page.locator('tr', { hasText: memberEmail });
    await expect(updatedUserRow.getByText('StoreOperator')).toBeVisible();
  });

  test('3. Cơ chế bảo vệ (Safeguard): Admin không thể tự tước quyền Quản trị viên của chính mình', async ({ page }) => {
    // Đăng nhập Admin
    await adminLogin(page);

    // Vào trang Người dùng
    await page.goto('/Admin/Users');
    const adminRow = page.locator('tr', { hasText: adminEmail });
    await expect(adminRow.getByText('Bạn (Đang đăng nhập)')).toBeVisible();

    // Bấm phân vai trò tài khoản admin hiện tại
    await adminRow.getByRole('link', { name: 'Phân vai trò' }).click();

    // Bỏ tích chọn quyền Admin (role_Admin)
    await page.uncheck('input#role_Admin');
    await page.getByRole('button', { name: 'Lưu thay đổi phân quyền' }).click();

    // Kiểm tra thông báo lỗi bảo vệ hiển thị, không cho phép lưu
    await expect(page.getByText('Bạn không thể tự gỡ quyền Quản trị viên (Admin) của chính tài khoản mình đang sử dụng.')).toBeVisible();
  });
});
