/* =====================================================================
   THAY / THÊM ẢNH SẢN PHẨM BẰNG SQL
   ---------------------------------------------------------------------
   Script này tạo thủ tục dbo.usp_SetProductImage: nạp 1 file ảnh trên máy
   (jpg, jpeg, png, webp, gif) vào bảng ProductImages của 1 sản phẩm.

   CÁCH DÙNG (làm 1 lần cho mỗi ảnh):
     1) Chép ảnh vào thư mục  C:\TVHouseImages  (xem HUONG_DAN_THAY_ANH.md để cấp quyền đọc)
     2) Chạy toàn bộ script này để tạo thủ tục (chỉ cần chạy 1 lần)
     3) Chạy lệnh EXEC ở phần "VÍ DỤ" bên dưới, sửa Id sản phẩm và đường dẫn file

   Lưu ý: ảnh nên < 3 MB cho nhẹ (web upload cũng giới hạn 3 MB/ảnh).
   ===================================================================== */

USE TVHouseDB;
GO

CREATE OR ALTER PROCEDURE dbo.usp_SetProductImage
    @ProductId INT,              -- Id sản phẩm cần gán ảnh (xem bằng: SELECT Id, Name FROM dbo.Products)
    @FilePath  NVARCHAR(500),    -- đường dẫn file ảnh TRÊN MÁY CHẠY SQL SERVER, ví dụ N'C:\TVHouseImages\tv1.jpg'
    @Replace   BIT = 1           -- 1 = THAY (xóa ảnh cũ của sản phẩm rồi gán ảnh mới); 0 = THÊM thêm 1 ảnh nữa
AS
BEGIN
    SET NOCOUNT ON;

    -- 1) Sản phẩm có tồn tại không?
    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId)
    BEGIN
        RAISERROR(N'Không tìm thấy sản phẩm có Id = %d.', 16, 1, @ProductId);
        RETURN;
    END

    -- 2) Đuôi file -> loại ảnh (ContentType) để trình duyệt hiển thị đúng
    DECLARE @ext NVARCHAR(10) = LOWER(CASE WHEN CHARINDEX(N'.', @FilePath) = 0 THEN N''
                                           ELSE RIGHT(@FilePath, CHARINDEX(N'.', REVERSE(@FilePath))) END);
    DECLARE @ctype VARCHAR(100) = CASE @ext
        WHEN N'.jpg'  THEN 'image/jpeg'
        WHEN N'.jpeg' THEN 'image/jpeg'
        WHEN N'.png'  THEN 'image/png'
        WHEN N'.webp' THEN 'image/webp'
        WHEN N'.gif'  THEN 'image/gif'
    END;
    IF @ctype IS NULL
    BEGIN
        RAISERROR(N'Chỉ hỗ trợ file .jpg, .jpeg, .png, .webp, .gif', 16, 1);
        RETURN;
    END

    -- 3) Tên file (bỏ phần thư mục) để lưu vào cột FileName
    DECLARE @fname NVARCHAR(200) = LEFT(
        CASE WHEN CHARINDEX(N'\', @FilePath) = 0 THEN @FilePath
             ELSE RIGHT(@FilePath, CHARINDEX(N'\', REVERSE(@FilePath)) - 1) END, 200);

    -- 4) Câu lệnh đọc nội dung file ảnh. OPENROWSET(BULK ...) bắt buộc đường dẫn là chuỗi cố định
    --    nên phải ghép vào câu lệnh động (nhân đôi dấu nháy đơn để tránh lỗi/chèn mã).
    DECLARE @sql NVARCHAR(MAX) = N'
        INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder)
        SELECT @pid, @fname, @ctype, f.BulkColumn, @sort
        FROM OPENROWSET(BULK N''' + REPLACE(@FilePath, N'''', N'''''') + N''', SINGLE_BLOB) AS f;';

    DECLARE @sort INT = 0;

    -- 5) Làm trong 1 giao dịch: nếu đọc file lỗi (sai đường dẫn / thiếu quyền) thì ảnh cũ KHÔNG bị xóa
    BEGIN TRY
        BEGIN TRAN;

        IF @Replace = 1
            DELETE FROM dbo.ProductImages WHERE ProductId = @ProductId;           -- thay: xóa ảnh cũ
        ELSE
            SELECT @sort = ISNULL(MAX(SortOrder), -1) + 1                           -- thêm: xếp sau các ảnh hiện có
            FROM dbo.ProductImages WHERE ProductId = @ProductId;

        EXEC sp_executesql @sql,
             N'@pid INT, @fname NVARCHAR(200), @ctype VARCHAR(100), @sort INT',
             @pid = @ProductId, @fname = @fname, @ctype = @ctype, @sort = @sort;

        COMMIT;
        PRINT N'Đã cập nhật ảnh cho sản phẩm Id = ' + CAST(@ProductId AS NVARCHAR(10)) + N' (' + @fname + N').';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* ---------------------------------------------------------------------
   XEM DANH SÁCH SẢN PHẨM VÀ SỐ ẢNH HIỆN CÓ (để biết Id cần thay)
   --------------------------------------------------------------------- */
SELECT p.Id, p.Name, p.Model, COUNT(i.Id) AS SoAnh
FROM dbo.Products p
LEFT JOIN dbo.ProductImages i ON i.ProductId = p.Id
GROUP BY p.Id, p.Name, p.Model
ORDER BY p.Id;
GO

/* ---------------------------------------------------------------------
   VÍ DỤ - bỏ dấu -- ở đầu dòng rồi sửa Id + đường dẫn cho đúng ảnh của bạn
   --------------------------------------------------------------------- */

-- THAY ảnh sản phẩm Id = 1 (xóa ảnh cũ, dùng ảnh mới):
-- EXEC dbo.usp_SetProductImage @ProductId = 1, @FilePath = N'C:\TVHouseImages\samsung-ua55du8000.jpg';

-- THÊM ảnh thứ 2 cho sản phẩm Id = 1 (giữ ảnh cũ):
-- EXEC dbo.usp_SetProductImage @ProductId = 1, @FilePath = N'C:\TVHouseImages\samsung-mat-sau.jpg', @Replace = 0;

-- Thay ảnh cho nhiều sản phẩm một lúc: viết nhiều dòng EXEC liên tiếp
-- EXEC dbo.usp_SetProductImage 2, N'C:\TVHouseImages\lg-oled55.png';
-- EXEC dbo.usp_SetProductImage 3, N'C:\TVHouseImages\sony-65.webp';

-- XÓA hết ảnh của 1 sản phẩm (cửa hàng sẽ hiện hình tivi mặc định):
-- DELETE FROM dbo.ProductImages WHERE ProductId = 1;

-- KIỂM TRA ảnh đã vào chưa (DATALENGTH = dung lượng byte):
-- SELECT Id, ProductId, FileName, ContentType, DATALENGTH(ImageData) AS Bytes, SortOrder FROM dbo.ProductImages WHERE ProductId = 1;
GO
