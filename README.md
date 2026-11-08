# TV House — ASP.NET Core MVC + SQL Server

Cửa hàng Tivi chạy trên **ASP.NET Core MVC (.NET 10)** + **Entity Framework Core** + **SQL Server**.
Mọi dữ liệu (sản phẩm, ảnh, đơn hàng, hóa đơn, nhân viên, mã giảm giá...) lưu trong database `TVHouseDB`.

## Cách chạy
1. Mở `Database/TVHouse.sql` trong SSMS / Azure Data Studio (kết nối `localhost\SQLEXPRESS`) → Execute.
   Script tạo database, các bảng và dữ liệu mẫu. **Chạy lại sẽ xóa và tạo lại toàn bộ bảng** (dữ liệu nhập trên web sẽ mất).
2. Chuỗi kết nối trong `appsettings.json` đang là `Server=localhost\SQLEXPRESS;...` (Windows Authentication).
3. `dotnet run` rồi mở https://localhost:7101 (hoặc mở `.csproj` bằng Visual Studio, F5).

## Đăng nhập trang Quản trị
- Bấm **Đăng nhập** ở góc trên bên phải. Tài khoản mặc định: **`admin`** / **`Admin@123`**.
- **Hãy đổi mật khẩu ngay** (Quản trị → "Đổi mật khẩu"). Mật khẩu lưu trong bảng `AdminUsers` dưới dạng băm PBKDF2.
- Nhập sai 5 lần sẽ bị khóa tạm 10 phút. Cửa hàng, giỏ hàng, xuất hóa đơn vẫn dùng được **không cần đăng nhập**; chỉ trang `/Admin` bị khóa.
- Quên mật khẩu: chạy lại `TVHouse.sql` (đặt lại về `Admin@123`, nhưng xóa dữ liệu) hoặc nhờ người có quyền SQL sửa cột `PasswordHash`.

## Đổi ảnh sản phẩm
Xem file **HUONG_DAN_THAY_ANH.md** (trên web hoặc bằng SQL `Database/ThayAnhSanPham.sql`).

## Các bảng trong SQL
| Bảng | Nội dung |
|---|---|
| **AdminUsers** | Tài khoản đăng nhập Quản trị (mật khẩu băm) |
| Categories, Products | Danh mục, sản phẩm |
| **ProductImages** | Ảnh sản phẩm (lưu VARBINARY, nối `Products` qua `ProductId`, nhiều ảnh / sản phẩm) |
| **Suppliers** | Nhà cung cấp (mã NCC001...) |
| **Employees** | Nhân viên bán hàng (mã NV001...) |
| Customers | Khách hàng (tự tạo/cập nhật khi xuất hóa đơn) |
| **DiscountCodes** | Mã giảm giá 20–50%, theo sản phẩm hoặc toàn bộ |
| Orders, OrderItems | Đơn hàng (có nhân viên tư vấn, mã giảm giá, phương thức thanh toán) |
| Invoices | Hóa đơn |
| ImportReceipts | Phiếu nhập hàng (có nhà cung cấp) |

## Chức năng
- **Cửa hàng**: sản phẩm + ảnh đọc từ SQL; lọc, tìm kiếm, sắp xếp.
- **Giỏ hàng → "Thanh toán & xuất hóa đơn"**: nhập thông tin khách mua tại quầy, chọn nhân viên tư vấn,
  phương thức thanh toán, nhập mã giảm giá (nút "Áp dụng" để xem trước) → xuất hóa đơn in được, có tên khách và tên nhân viên.
- **Quản trị**: Sản phẩm (nhiều ảnh) · Kho hàng (chỉ xem + tìm theo tên) · Nhập hàng · Nhà cung cấp ·
  Đơn hàng · Hóa đơn · Khách hàng · Nhân viên (kèm số hóa đơn/doanh thu tư vấn) · Mã giảm giá · Báo cáo.
- **Xuất file SQL**: Quản trị → Tổng quan → "Xuất file SQL" tải về file `.sql` gồm cấu trúc bảng + toàn bộ dữ liệu hiện tại.

## Quy tắc tồn kho
Kho chỉ thay đổi qua: phiếu nhập (+), hóa đơn/đơn hàng (−), hủy đơn (hoàn lại). Sản phẩm mới có tồn kho 0.

## Lưu ý
- Trước khi đưa website lên mạng: đổi mật khẩu admin, dùng HTTPS và đổi chuỗi kết nối sang tài khoản SQL có quyền hạn chế.
- Muốn dùng .NET 8: đổi `TargetFramework` thành `net8.0` và gói EF Core về `8.0.x` trong `TVHouse.Web.csproj`.
