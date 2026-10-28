# Bộ quy chuẩn Thiết kế Giao diện (Anti-AI-Slop Design System)

> **Mục tiêu:** Triệt tiêu hoàn toàn cảm giác "AI Slop" (màu tím indigo rực rỡ, bo góc `rounded-3xl` quá đà, bóng neon, card lơ lửng, emoji/icon sáo rỗng), đưa giao diện về chuẩn mực **Minimalist E-Commerce** (phong cách MedusaJS / Vercel Store / Apple).

---

## 1. Triết lý thiết kế (Design Philosophy)

1. **Product First (Sản phẩm là trung tâm):** Giao diện chỉ đóng vai trò làm nền (canvas) để tôn vinh hình ảnh, giá tiền và thông số sản phẩm. Không dùng các chi tiết đồ họa màu mè cạnh tranh thị giác với sản phẩm.
2. **Restraint & Precision (Tiết chế và chuẩn xác):** Tránh các khối card quá khổ, khoảng trắng giả tạo. Tăng mật độ thông tin (information density) để người dùng xem được nhiều sản phẩm/dòng đơn hàng trên một màn hình.
3. **Authentic Editorial Feel (Chất thực tế):** Loại bỏ các khẩu hiệu sáo rỗng do AI sinh ra (như *"Đỉnh cao công nghệ"*, *"Elevate your lifestyle"*). Nội dung giao diện hướng thẳng vào nhu cầu mua sắm thực tế.

---

## 2. Bảng mã màu chuẩn (Design Tokens)

Không sử dụng gradient tím/xanh ngẫu nhiên. Dự án chuẩn hóa theo bảng màu **Neutral & Obsidian**:

| Thành phần | Light Mode | Dark Mode | Ý nghĩa sử dụng |
|---|---|---|---|
| **Primary (Chính)** | `#18181B` (`zinc-900`) | `#F4F4F5` (`zinc-100`) | Nút CTA chính, tiêu đề quan trọng, logo |
| **Accent (Điểm nhấn)** | `#D97706` (`amber-600`) | `#F59E0B` (`amber-500`) | Nhãn giảm giá, cảnh báo tồn kho, rating |
| **Secondary Accent** | `#0284C7` (`sky-600`) | `#38BDF8` (`sky-400`) | Liên kết điều hướng phụ, tag danh mục |
| **Background (Nền)** | `#FAFAFA` (`zinc-50`) | `#09090B` (`zinc-950`) | Toàn bộ nền trang |
| **Surface (Card/Khối)** | `#FFFFFF` (Trắng tuyền) | `#18181B` (`zinc-900`) | Nền của card sản phẩm, modal, form |
| **Border (Đường viền)** | `#E4E4E7` (`zinc-200`) | `#27272A` (`zinc-800`) | Viền 1px chuẩn trên mọi card và bảng |
| **Muted Text** | `#71717A` (`zinc-500`) | `#A1A1AA` (`zinc-400`) | Mô tả phụ, nhãn phụ, ngày tháng |

---

## 3. Quy tắc hình học & Bo góc (Border Radius)

* ❌ **CẤM:** `rounded-3xl`, `rounded-2xl` trên các card thông thường; cấm `rounded-full` trên các nút bấm dài hoặc card.
* ✅ **QUY ĐỊNH:**
  * **Card sản phẩm, container, modal:** `rounded-lg` (8px).
  * **Nút bấm (Buttons), Form Input, Select:** `rounded-md` (6px).
  * **Badge trạng thái, Tag:** `rounded` (4px) hoặc `rounded-full` nhỏ (chỉ áp dụng cho dot indicator hoặc tag nhỏ 16-20px).

---

## 4. Đổ bóng & Nét viền (Elevation & Borders)

* ❌ **CẤM:** Bóng đổ neon có màu (`shadow-indigo-500/30`), bóng mờ khổng lồ (`shadow-2xl`), hiệu ứng `backdrop-blur` giả tạo nếu không cần thiết.
* ✅ **QUY ĐỊNH:**
  * Luôn sử dụng viền thực: `border border-zinc-200 dark:border-zinc-800`.
  * Trạng thái thường: `shadow-sm` hoặc không bóng.
  * Trạng thái Hover: `hover:border-zinc-400 dark:hover:border-zinc-600` kèm `hover:shadow-md`. Bỏ hiệu ứng nhảy card quá trớn (`hover:-translate-y-2`).

---

## 5. Typography & Trưng bày dữ liệu

* **Font chữ:** `Inter` hoặc `Plus Jakarta Sans` với `tracking-tight` cho các tiêu đề lớn.
* **Giá tiền:** Luôn in đậm, tương phản rõ rệt, căn chỉnh số liệu thẳng hàng.
* **Icon:** Không lạm dụng icon trang trí vô nghĩa (`fa-sparkles`, `fa-bolt`). Chỉ dùng icon biểu thị đúng chức năng (giỏ hàng, tìm kiếm, mắt xem chi tiết, ngôi sao rating).

---

## 6. Quy chuẩn Chuyển động & Hoạt họa (Motion & Micro-interactions)

> **Nguyên tắc cốt lõi:** Functional Motion (Chuyển động có mục đích). Animation phải nhanh, dứt khoát, phục vụ phản hồi xúc giác cho người dùng thay vì "múa lượn" biểu diễn.

### 6.1 Thời gian & Đường cong chuyển động (Timing & Easing)
* **Thời lượng tối ưu:**
  * Phản hồi click chuột / active: **100ms – 120ms** (`duration-100`).
  * Hover / Focus / Color swap: **150ms** (`duration-150`).
  * Dropdown, Dialog, Drawer mở ra: **200ms – 250ms** (`duration-200`).
  * ❌ **CẤM:** Các hiệu ứng chuyển động kéo dài trên 300ms cho các thao tác thông thường.
* **Đường cong chuyển động (Easing):**
  * Ưu tiên đường cong hãm phanh mượt mà (Deceleration Curve) chuẩn Apple/iOS: `cubic-bezier(0.16, 1, 0.3, 1)`.
  * Tránh dùng `ease-in-out` mặc định của trình duyệt cho các thành phần UI nhỏ.

### 6.2 Các Micro-interactions chuẩn mực
1. **Phản hồi bấm nút xúc giác (Tactile Press):**
   * Sử dụng `active:scale-[0.98]` trên các nút bấm chính (`button`, link CTA). Giúp người dùng cảm nhận được nút đã nhận lệnh bấm vật lý chắc chắn.
2. **Phóng to ảnh nội bộ (Internal Image Zoom):**
   * Đặt ảnh bên trong khung bọc `overflow-hidden`. Khi hover, ảnh chỉ phóng to nhẹ (`group-hover:scale-105 transition-transform duration-500 ease-out`), **không** làm khung card bên ngoài bị nhảy giật vị trí (`hover:-translate-y-2`).
3. **Cập nhật giỏ hàng êm dịu:**
   * Tận dụng thư viện siêu nhẹ `@formkit/auto-animate` (2KB) trên container danh sách giỏ hàng. Khi xóa hoặc thay đổi số lượng món hàng, các dòng tự động trượt lấp chỗ trống mượt mà thay vì giật layout.
4. **Hỗ trợ Tiếp cận (Accessibility / Reduced Motion):**
   * Luôn tôn trọng thiết lập của người dùng: với người dùng bật `prefers-reduced-motion: reduce`, tắt toàn bộ transform chuyển động, chỉ giữ lại chuyển đổi độ trong suốt `opacity`.
