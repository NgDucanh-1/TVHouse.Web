# Hướng dẫn thay ảnh cho Tivi

Ảnh sản phẩm được lưu trong SQL Server, bảng **`ProductImages`** (nối với bảng `Products` qua cột `ProductId`).
Có 2 cách thay ảnh. **Cách 1 (trên web) là dễ nhất.**

---
## Cách 1 — Thay ảnh ngay trên web (khuyên dùng)

1. Chạy web, bấm **Đăng nhập** (mặc định: `admin` / `Admin@123`).
2. Vào **Quản trị → Sản phẩm**, bấm **Sửa** ở tivi cần đổi ảnh.
3. Kéo xuống phần **Ảnh sản phẩm**:
   - Muốn bỏ ảnh cũ: tick **"Xóa ảnh này"** dưới ảnh đó.
   - Muốn thêm ảnh mới: bấm **Chọn tệp** và chọn 1 hoặc nhiều ảnh (JPG, PNG, WEBP, GIF, tối đa 3 MB/ảnh). Ảnh vừa chọn sẽ hiện xem trước.
4. Bấm **Lưu sản phẩm**.

Ảnh đầu tiên trong danh sách là **ảnh chính** hiển thị ở trang chủ. Muốn đổi ảnh chính: xóa ảnh chính cũ (hoặc xóa hết) rồi tải ảnh mới lên.
Ảnh được ghi thẳng vào SQL Server, không cần chép file vào thư mục web.

---
## Cách 2 — Thay ảnh bằng SQL (file `Database/ThayAnhSanPham.sql`)

Dùng khi bạn muốn nạp hàng loạt ảnh từ 1 thư mục.

### Bước 1. Chuẩn bị thư mục ảnh và cấp quyền đọc cho SQL Server
SQL Server chạy bằng một tài khoản dịch vụ riêng nên **không đọc được** ảnh trong Desktop / Documents / Downloads. Hãy tạo thư mục riêng, ví dụ `C:\TVHouseImages`, chép ảnh vào, rồi cấp quyền (mở **PowerShell** bằng quyền Administrator):

```powershell
icacls 'C:\TVHouseImages' /grant 'NT Service\MSSQL$SQLEXPRESS:(OI)(CI)R'
```
(`MSSQL$SQLEXPRESS` là tên dịch vụ của SQL Server Express. Nếu bạn dùng instance khác, tên dịch vụ có dạng `MSSQL$TÊN_INSTANCE`; instance mặc định là `MSSQLSERVER`.)

### Bước 2. Tạo thủ tục (chạy 1 lần)
Mở `ThayAnhSanPham.sql` trong Azure Data Studio / SSMS, kết nối `localhost\SQLEXPRESS`, bấm **Run**.
Kết quả cuối script là bảng liệt kê **Id** và tên từng sản phẩm để bạn biết cần thay ảnh cho Id nào.

### Bước 3. Gán ảnh
Chạy các lệnh sau (sửa Id và đường dẫn):

```sql
USE TVHouseDB;
-- THAY ảnh của sản phẩm Id = 1 (ảnh cũ bị xóa):
EXEC dbo.usp_SetProductImage @ProductId = 1, @FilePath = N'C:\TVHouseImages\samsung-55.jpg';

-- THÊM 1 ảnh nữa cho sản phẩm Id = 1 (giữ ảnh cũ):
EXEC dbo.usp_SetProductImage @ProductId = 1, @FilePath = N'C:\TVHouseImages\samsung-mat-sau.jpg', @Replace = 0;
```
Tải lại trang chủ (Ctrl+F5) là thấy ảnh mới. Nếu ảnh vẫn cũ, đợi tối đa 1 giờ hoặc xóa cache trình duyệt (trình duyệt lưu đệm ảnh 1 giờ).

### Nếu gặp lỗi
| Thông báo | Nguyên nhân / cách xử lý |
|---|---|
| `Cannot bulk load because the file ... could not be opened. Operating system error code 5 (Access is denied)` | SQL Server chưa có quyền đọc thư mục → làm lại Bước 1 (đặt ảnh ở `C:\TVHouseImages`). |
| `... error code 3 (The system cannot find the path specified)` | Sai đường dẫn / tên file → kiểm tra lại. |
| `You do not have permission to use the bulk load statement` | Tài khoản đăng nhập SQL cần quyền `ADMINISTER BULK OPERATIONS` (dùng tài khoản quản trị / sysadmin). |
| `Chỉ hỗ trợ file .jpg, .jpeg, .png, .webp, .gif` | Đổi sang đúng định dạng ảnh. |

Khi lỗi, ảnh cũ **không bị xóa** (thủ tục làm trong một giao dịch và tự hoàn tác).

---
## Muốn ảnh nằm sẵn trong file `TVHouse.sql`?
File `TVHouse.sql` chỉ để cài đặt ban đầu (ảnh mẫu là hình vẽ SVG). Sau khi bạn đã gán ảnh thật bằng Cách 1 hoặc Cách 2, vào **Quản trị → Tổng quan → "Xuất file SQL"**: file tải về chứa cấu trúc bảng + toàn bộ dữ liệu + **ảnh thật** của bạn (dạng mã hex). Dùng file đó thay cho `TVHouse.sql` khi cài sang máy khác.
> Lưu ý: chạy lại `TVHouse.sql` gốc sẽ xóa ảnh và dữ liệu bạn đã đổi, nên hãy xuất file SQL sao lưu trước.
