import { test, expect } from '@playwright/test';

// Helper: card sản phẩm TechNova không có class .card — định vị qua h3 tên sản phẩm.
const productHeading = (page: import('@playwright/test').Page, name: string) =>
  page.locator('h3', { hasText: name });

test.describe('Catalog & Product Management Flow (Phase 3)', () => {
  test('1. Khách hàng xem danh sách sản phẩm và xem chi tiết sản phẩm', async ({ page }) => {
    await page.goto('/Products');

    await expect(page.getByRole('heading', { name: 'Danh sách sản phẩm' })).toBeVisible();
    await expect(productHeading(page, 'Mechanical Keyboard')).toBeVisible();

    // Bấm vào tên sản phẩm để xem chi tiết
    await productHeading(page, 'Mechanical Keyboard').getByRole('link').click();

    await expect(page).toHaveURL(/\/Products\/Details\/\d+/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Mechanical Keyboard');
    await expect(page.getByText('$89.50')).toBeVisible();
    await expect(page.getByText('Electronics').first()).toBeVisible();
    await expect(page.getByText(/Còn hàng/)).toBeVisible();
  });

  test('2. Khách hàng tìm kiếm sản phẩm theo từ khóa và lọc theo danh mục', async ({ page }) => {
    await page.goto('/Products');

    // Tìm kiếm với từ khóa "Keyboard" (submit form bằng Enter vì nút tìm là icon)
    await page.fill('aside input[name="search"]', 'Keyboard');
    await page.press('aside input[name="search"]', 'Enter');

    await expect(productHeading(page, 'Mechanical Keyboard')).toBeVisible();
    await expect(productHeading(page, 'Cotton T-Shirt')).not.toBeVisible();

    // Lọc theo danh mục "Clothing" bằng link danh mục ở sidebar
    await page.goto('/Products');
    await page.locator('aside').getByRole('link', { name: 'Clothing' }).click();

    await expect(productHeading(page, 'Cotton T-Shirt')).toBeVisible();
    await expect(productHeading(page, 'Denim Jacket')).toBeVisible();
    await expect(productHeading(page, 'Mechanical Keyboard')).not.toBeVisible();
  });

  test('3. Admin quản lý CRUD Category, CRUD Product và kích hoạt / lưu kho sản phẩm', async ({ page }) => {
    // 1. Đăng nhập tài khoản Admin
    await page.goto('/Account/Login');
    await page.fill('input[name="Email"]', 'admin@ecommerce.local');
    await page.fill('input[name="Password"]', 'Admin@12345');
    await page.locator('main button[type="submit"]').click();
    await expect(page).toHaveURL('/');

    // 2. Thêm Danh mục mới
    await page.goto('/Admin/Categories');
    await page.getByRole('link', { name: 'Thêm danh mục mới' }).click();
    await expect(page).toHaveURL('/Admin/CreateCategory');

    const uniqueId = Date.now();
    const testCategoryName = `Audio ${uniqueId}`;
    await page.fill('input[name="Name"]', testCategoryName);
    await page.fill('textarea[name="Description"]', 'Tai nghe và thiết bị âm thanh');
    await page.getByRole('button', { name: 'Tạo danh mục' }).click();

    await expect(page).toHaveURL('/Admin/Categories');
    await expect(page.getByRole('cell', { name: testCategoryName })).toBeVisible();

    // 3. Thêm Sản phẩm mới thuộc danh mục vừa tạo (nút submit thực là "Tạo sản phẩm")
    await page.goto('/Admin/Products');
    await page.getByRole('link', { name: 'Thêm sản phẩm mới' }).click();
    await expect(page).toHaveURL('/Admin/CreateProduct');

    const testProductName = `Hi-Fi Headphones ${uniqueId}`;
    await page.fill('input[name="Name"]', testProductName);
    await page.selectOption('select[name="CategoryId"]', { label: testCategoryName });
    await page.fill('input[name="Price"]', '149.99');
    await page.fill('input[name="StockQuantity"]', '25');
    await page.fill('textarea[name="Description"]', 'Tai nghe chống ồn chủ động cao cấp.');
    await page.getByRole('button', { name: 'Tạo sản phẩm' }).click();

    await expect(page).toHaveURL('/Admin/Products');
    await expect(page.getByRole('cell', { name: testProductName })).toBeVisible();

    // 4. Khách hàng thấy sản phẩm mới ngoài catalog
    await page.goto(`/Products?search=${uniqueId}`);
    await expect(productHeading(page, testProductName)).toBeVisible();

    // 5. Admin lưu kho (deactivate / archive) sản phẩm
    await page.goto('/Admin/Products');
    const productRow = page.locator('tr', { hasText: testProductName });
    await productRow.getByRole('button', { name: 'Lưu kho' }).click();
    await expect(page.getByText(`Đã lưu kho / ngưng kinh doanh sản phẩm '${testProductName}'.`)).toBeVisible();

    // 6. Kiểm tra sản phẩm đã bị ẩn khỏi phía Khách hàng
    await page.goto(`/Products?search=${uniqueId}`);
    await expect(productHeading(page, testProductName)).not.toBeVisible();
    await expect(page.getByText('Không tìm thấy sản phẩm nào')).toBeVisible();

    // 7. Admin kích hoạt (activate) sản phẩm trở lại
    await page.goto('/Admin/Products');
    const inactiveRow = page.locator('tr', { hasText: testProductName });
    await inactiveRow.getByRole('button', { name: 'Kích hoạt' }).click();
    await expect(page.getByText(`Đã kích hoạt kinh doanh sản phẩm '${testProductName}'.`)).toBeVisible();

    // 8. Khách hàng lại thấy sản phẩm xuất hiện
    await page.goto(`/Products?search=${uniqueId}`);
    await expect(productHeading(page, testProductName)).toBeVisible();
  });
});
