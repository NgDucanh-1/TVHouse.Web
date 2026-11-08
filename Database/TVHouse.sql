/* =====================================================================
   TV HOUSE - Cơ sở dữ liệu SQL Server (phiên bản 2)
   ---------------------------------------------------------------------
   Cách chạy:
     - SSMS / Azure Data Studio: mở file này và bấm Execute (F5)
     - Hoặc dòng lệnh:  sqlcmd -S localhost\SQLEXPRESS -E -i TVHouse.sql
   Lưu ý:
     - Script có thể chạy lại nhiều lần: sẽ XÓA và tạo lại toàn bộ bảng
       cùng dữ liệu mẫu (không xóa cả database). Dữ liệu bạn đã nhập trên
       web sẽ MẤT -> hãy dùng nút "Xuất file SQL" trong trang Quản trị
       để sao lưu trước khi chạy lại.
     - Tên database: TVHouseDB (khớp với appsettings.json của web).
     - Web ghi dữ liệu trực tiếp vào database này. Nút "Xuất file SQL"
       trong trang Quản trị tạo ra file .sql mới = cấu trúc bảng (phần
       trên dòng BEGIN SEED DATA của file này) + toàn bộ dữ liệu hiện tại.
   ===================================================================== */

USE master;
GO

IF DB_ID(N'TVHouseDB') IS NULL
    CREATE DATABASE TVHouseDB COLLATE Vietnamese_CI_AS;
GO

USE TVHouseDB;
GO

-- Cột tính toán (PERSISTED) yêu cầu hai tùy chọn này phải bật
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------
   1. XÓA BẢNG CŨ (theo thứ tự khóa ngoại)
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AdminUsers',     N'U') IS NOT NULL DROP TABLE dbo.AdminUsers;
IF OBJECT_ID(N'dbo.ImportReceipts', N'U') IS NOT NULL DROP TABLE dbo.ImportReceipts;
IF OBJECT_ID(N'dbo.Invoices',       N'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID(N'dbo.OrderItems',     N'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID(N'dbo.Orders',         N'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID(N'dbo.DiscountCodes',  N'U') IS NOT NULL DROP TABLE dbo.DiscountCodes;
IF OBJECT_ID(N'dbo.Customers',      N'U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID(N'dbo.Employees',      N'U') IS NOT NULL DROP TABLE dbo.Employees;
IF OBJECT_ID(N'dbo.Suppliers',      N'U') IS NOT NULL DROP TABLE dbo.Suppliers;
IF OBJECT_ID(N'dbo.ProductImages',  N'U') IS NOT NULL DROP TABLE dbo.ProductImages;
IF OBJECT_ID(N'dbo.Products',       N'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID(N'dbo.Categories',     N'U') IS NOT NULL DROP TABLE dbo.Categories;
GO

/* ---------------------------------------------------------------------
   2. TẠO BẢNG
   --------------------------------------------------------------------- */

-- Tài khoản đăng nhập trang Quản trị.
-- Mật khẩu KHÔNG lưu dạng chữ thường mà lưu dạng băm PBKDF2 (không thể dịch ngược).
-- FailedAttempts / LockoutEnd: nhập sai 5 lần sẽ bị khóa tạm 10 phút (chống dò mật khẩu).
CREATE TABLE dbo.AdminUsers (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY,
    Username        NVARCHAR(50)      NOT NULL CONSTRAINT UQ_AdminUsers_Username UNIQUE,
    PasswordHash    VARCHAR(300)      NOT NULL,
    DisplayName     NVARCHAR(100)     NOT NULL,
    IsActive        BIT               NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT (1),
    FailedAttempts  INT               NOT NULL CONSTRAINT DF_AdminUsers_Failed DEFAULT (0),
    LockoutEnd      DATETIME2(0)      NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_AdminUsers_CreatedAt DEFAULT (SYSDATETIME())
);

-- Danh mục sản phẩm (Tivi LED / OLED / QLED)
CREATE TABLE dbo.Categories (
    Id    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name  NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE
);

-- Sản phẩm
CREATE TABLE dbo.Products (
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name             NVARCHAR(200)     NOT NULL,
    Brand            NVARCHAR(50)      NOT NULL,
    Model            NVARCHAR(50)      NOT NULL,
    CategoryId       INT               NOT NULL CONSTRAINT FK_Products_Categories REFERENCES dbo.Categories(Id),
    ScreenSize       INT               NOT NULL CONSTRAINT CK_Products_ScreenSize CHECK (ScreenSize > 0),
    Resolution       NVARCHAR(30)      NOT NULL,
    OperatingSystem  NVARCHAR(30)      NOT NULL,
    WarrantyMonths   INT               NOT NULL CONSTRAINT DF_Products_Warranty DEFAULT (24),
    Price            DECIMAL(18,0)     NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    OldPrice         DECIMAL(18,0)     NOT NULL,
    Stock            INT               NOT NULL CONSTRAINT DF_Products_Stock DEFAULT (0)
                                                CONSTRAINT CK_Products_Stock CHECK (Stock >= 0),
    IsFeatured       BIT               NOT NULL CONSTRAINT DF_Products_IsFeatured DEFAULT (0),
    IsActive         BIT               NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),   -- 0 = đã xóa (ẩn)
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSDATETIME())
);
CREATE INDEX IX_Products_CategoryId ON dbo.Products(CategoryId);

-- Ảnh sản phẩm: BẢNG RIÊNG, nối với Products qua ProductId (1 sản phẩm có nhiều ảnh).
-- Ảnh được lưu trực tiếp trong SQL Server (VARBINARY). Ảnh có SortOrder nhỏ nhất là ảnh chính.
CREATE TABLE dbo.ProductImages (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductImages PRIMARY KEY,
    ProductId    INT               NOT NULL CONSTRAINT FK_ProductImages_Products REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    FileName     NVARCHAR(200)     NOT NULL,
    ContentType  VARCHAR(100)      NOT NULL,
    ImageData    VARBINARY(MAX)    NOT NULL,
    SortOrder    INT               NOT NULL CONSTRAINT DF_ProductImages_Sort DEFAULT (0),
    CreatedAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_ProductImages_CreatedAt DEFAULT (SYSDATETIME())
);
CREATE INDEX IX_ProductImages_ProductId ON dbo.ProductImages(ProductId, SortOrder);

-- Nhà cung cấp (mã NCC001... tự sinh)
CREATE TABLE dbo.Suppliers (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
    SupplierCode   AS ('NCC' + REPLICATE('0', CASE WHEN Id < 100 THEN 3 - LEN(CONVERT(VARCHAR(10), Id)) ELSE 0 END)
                          + CONVERT(VARCHAR(10), Id)) PERSISTED,
    Name           NVARCHAR(150)     NOT NULL,
    ContactPerson  NVARCHAR(100)     NULL,
    Phone          VARCHAR(20)       NULL,
    Email          NVARCHAR(100)     NULL,
    Address        NVARCHAR(250)     NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT (1),
    CreatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT (SYSDATETIME())
);

-- Nhân viên bán hàng (mã NV001... tự sinh) - người tư vấn cho khách trên hóa đơn
CREATE TABLE dbo.Employees (
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    EmployeeCode  AS ('NV' + REPLICATE('0', CASE WHEN Id < 100 THEN 3 - LEN(CONVERT(VARCHAR(10), Id)) ELSE 0 END)
                         + CONVERT(VARCHAR(10), Id)) PERSISTED,
    FullName      NVARCHAR(100)     NOT NULL,
    Phone         VARCHAR(20)       NULL,
    Email         NVARCHAR(100)     NULL,
    Position      NVARCHAR(50)      NOT NULL,
    HireDate      DATE              NOT NULL CONSTRAINT DF_Employees_HireDate DEFAULT (CONVERT(DATE, SYSDATETIME())),
    IsActive      BIT               NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1)   -- 0 = đã nghỉ việc
);

-- Khách hàng
CREATE TABLE dbo.Customers (
    Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    FullName  NVARCHAR(100)     NOT NULL,
    Email     NVARCHAR(100)     NULL,
    Phone     VARCHAR(20)       NULL
);

-- Mã giảm giá: giảm từ 20% đến 50%, áp dụng cho 1 sản phẩm (ProductId) hoặc toàn bộ (ProductId = NULL)
CREATE TABLE dbo.DiscountCodes (
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountCodes PRIMARY KEY,
    Code             NVARCHAR(30)      NOT NULL CONSTRAINT UQ_DiscountCodes_Code UNIQUE,
    DiscountPercent  INT               NOT NULL CONSTRAINT CK_Discount_Percent CHECK (DiscountPercent BETWEEN 20 AND 50),
    ProductId        INT               NULL CONSTRAINT FK_Discount_Products REFERENCES dbo.Products(Id),
    StartDate        DATE              NOT NULL,
    EndDate          DATE              NOT NULL,
    UsageLimit       INT               NULL CONSTRAINT CK_Discount_Limit CHECK (UsageLimit IS NULL OR UsageLimit > 0),
    UsedCount        INT               NOT NULL CONSTRAINT DF_Discount_Used DEFAULT (0),
    IsActive         BIT               NOT NULL CONSTRAINT DF_Discount_IsActive DEFAULT (1),
    CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Discount_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Discount_Dates CHECK (EndDate >= StartDate)
);

-- Đơn hàng (mã đơn DHxxxx tự sinh). EmployeeId = nhân viên tư vấn.
CREATE TABLE dbo.Orders (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    OrderCode       AS ('DH' + REPLICATE('0', CASE WHEN Id < 1000 THEN 4 - LEN(CONVERT(VARCHAR(10), Id)) ELSE 0 END)
                           + CONVERT(VARCHAR(10), Id)) PERSISTED,
    CustomerId      INT               NULL CONSTRAINT FK_Orders_Customers REFERENCES dbo.Customers(Id) ON DELETE SET NULL,
    EmployeeId      INT               NULL CONSTRAINT FK_Orders_Employees REFERENCES dbo.Employees(Id) ON DELETE SET NULL,
    OrderDate       DATETIME2(0)      NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT (SYSDATETIME()),
    SubtotalAmount  DECIMAL(18,0)     NOT NULL CONSTRAINT CK_Orders_Subtotal CHECK (SubtotalAmount >= 0),
    DiscountCodeId  INT               NULL CONSTRAINT FK_Orders_DiscountCodes REFERENCES dbo.DiscountCodes(Id),
    DiscountAmount  DECIMAL(18,0)     NOT NULL CONSTRAINT DF_Orders_Discount DEFAULT (0),
    TotalAmount     DECIMAL(18,0)     NOT NULL CONSTRAINT CK_Orders_Total CHECK (TotalAmount >= 0),
    PaymentMethod   NVARCHAR(30)      NOT NULL CONSTRAINT DF_Orders_Payment DEFAULT (N'Tiền mặt'),
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Orders_Status DEFAULT ('cho_xac_nhan')
                    CONSTRAINT CK_Orders_Status CHECK (Status IN ('cho_xac_nhan','da_xac_nhan','dang_giao','hoan_thanh','da_huy')),
    CONSTRAINT CK_Orders_Amounts CHECK (TotalAmount = SubtotalAmount - DiscountAmount)
);
CREATE INDEX IX_Orders_Status     ON dbo.Orders(Status);
CREATE INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
CREATE INDEX IX_Orders_EmployeeId ON dbo.Orders(EmployeeId);

-- Chi tiết đơn hàng
CREATE TABLE dbo.OrderItems (
    Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId    INT               NOT NULL CONSTRAINT FK_OrderItems_Orders   REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    ProductId  INT               NOT NULL CONSTRAINT FK_OrderItems_Products REFERENCES dbo.Products(Id),
    Quantity   INT               NOT NULL CONSTRAINT CK_OrderItems_Qty CHECK (Quantity > 0),
    UnitPrice  DECIMAL(18,0)     NOT NULL CONSTRAINT CK_OrderItems_Price CHECK (UnitPrice >= 0)
);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);

-- Hóa đơn bán hàng (mỗi đơn tối đa 1 hóa đơn)
CREATE TABLE dbo.Invoices (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    InvoiceCode  AS ('HD' + REPLICATE('0', CASE WHEN Id < 1000 THEN 4 - LEN(CONVERT(VARCHAR(10), Id)) ELSE 0 END)
                        + CONVERT(VARCHAR(10), Id)) PERSISTED,
    OrderId      INT               NOT NULL CONSTRAINT FK_Invoices_Orders REFERENCES dbo.Orders(Id) ON DELETE CASCADE
                                            CONSTRAINT UQ_Invoices_OrderId UNIQUE,
    InvoiceDate  DATETIME2(0)      NOT NULL CONSTRAINT DF_Invoices_Date DEFAULT (SYSDATETIME()),
    TotalAmount  DECIMAL(18,0)     NOT NULL CONSTRAINT CK_Invoices_Total CHECK (TotalAmount >= 0)
);

-- Phiếu nhập hàng (có nhà cung cấp)
CREATE TABLE dbo.ImportReceipts (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportReceipts PRIMARY KEY,
    ReceiptCode  AS ('PN' + REPLICATE('0', CASE WHEN Id < 100 THEN 3 - LEN(CONVERT(VARCHAR(10), Id)) ELSE 0 END)
                        + CONVERT(VARCHAR(10), Id)) PERSISTED,
    ImportDate   DATETIME2(0)      NOT NULL CONSTRAINT DF_Import_Date DEFAULT (SYSDATETIME()),
    SupplierId   INT               NOT NULL CONSTRAINT FK_Import_Suppliers REFERENCES dbo.Suppliers(Id),
    ProductId    INT               NOT NULL CONSTRAINT FK_Import_Products  REFERENCES dbo.Products(Id),
    Quantity     INT               NOT NULL CONSTRAINT CK_Import_Qty CHECK (Quantity > 0),
    UnitPrice    DECIMAL(18,0)     NOT NULL CONSTRAINT CK_Import_Price CHECK (UnitPrice >= 0)
);
CREATE INDEX IX_ImportReceipts_ProductId  ON dbo.ImportReceipts(ProductId);
CREATE INDEX IX_ImportReceipts_SupplierId ON dbo.ImportReceipts(SupplierId);
GO

/* ===== BEGIN SEED DATA ===== */

/* ---------------------------------------------------------------------
   3. DỮ LIỆU MẪU
   --------------------------------------------------------------------- */

-- Tài khoản quản trị mặc định: admin / Admin@123  (HÃY ĐỔI MẬT KHẨU NGAY SAU KHI ĐĂNG NHẬP LẦN ĐẦU)
SET IDENTITY_INSERT dbo.AdminUsers ON;
INSERT INTO dbo.AdminUsers (Id, Username, PasswordHash, DisplayName) VALUES
    (1, N'admin', 'PBKDF2$100000$ZMHYzDtFxpHc0ONXeFQOFQ==$/b4NgG4xIJNhHWHiuh5YlNe1HOVUCuyEYVhHt3r+a1A=', N'Quản trị viên');
SET IDENTITY_INSERT dbo.AdminUsers OFF;
GO

SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (Id, Name) VALUES
    (1, N'Tivi LED'),
    (2, N'Tivi OLED'),
    (3, N'Tivi QLED');
SET IDENTITY_INSERT dbo.Categories OFF;
GO

SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products (Id, Name, Brand, Model, CategoryId, ScreenSize, Resolution, OperatingSystem, WarrantyMonths, Price, OldPrice, Stock, IsFeatured) VALUES
    (1, N'Smart TV Samsung Crystal UHD 4K 55 inch', N'Samsung', N'UA55DU8000', 1, 55, N'4K UHD', N'Tizen', 24, 10990000, 13990000, 12, 1),
    (2, N'Smart TV LG OLED 4K 55 inch', N'LG', N'OLED55B4PSA', 2, 55, N'4K OLED', N'webOS', 24, 22990000, 26990000, 4, 1),
    (3, N'Google TV Sony 4K 65 inch', N'Sony', N'KD-65X75K', 1, 65, N'4K UHD', N'Google TV', 24, 16990000, 19990000, 8, 1),
    (4, N'Samsung QLED 4K 65 inch', N'Samsung', N'QA65Q60D', 3, 65, N'4K QLED', N'Tizen', 24, 18990000, 22990000, 3, 1),
    (5, N'Xiaomi TV A Pro QLED 55 inch', N'Xiaomi', N'L55MA-APSEA', 3, 55, N'4K QLED', N'Google TV', 24, 8990000, 10990000, 15, 0),
    (6, N'Smart TV TCL 4K 50 inch', N'TCL', N'50P755', 1, 50, N'4K UHD', N'Google TV', 24, 7990000, 9490000, 20, 0),
    (7, N'LG OLED evo 4K 65 inch', N'LG', N'OLED65C4PSA', 2, 65, N'4K OLED', N'webOS', 24, 39990000, 45990000, 2, 1),
    (8, N'Sony Bravia QLED 4K 55 inch', N'Sony', N'K-55S30', 3, 55, N'4K QLED', N'Google TV', 24, 14990000, 17990000, 9, 0),
    (9, N'Smart TV Samsung Crystal UHD 4K 43 inch', N'Samsung', N'UA43DU7700', 1, 43, N'4K UHD', N'Tizen', 24, 7490000, 8990000, 18, 0),
    (10, N'Smart TV LG UHD 4K 50 inch', N'LG', N'50UR8050PSB', 1, 50, N'4K UHD', N'webOS', 24, 8990000, 10490000, 14, 0),
    (11, N'Google TV Sony Bravia 4K 50 inch', N'Sony', N'K-50S25B', 1, 50, N'4K UHD', N'Google TV', 24, 11990000, 13990000, 7, 0),
    (12, N'Smart TV TCL QLED 4K 65 inch', N'TCL', N'65C655', 3, 65, N'4K QLED', N'Google TV', 24, 13990000, 16990000, 10, 1),
    (13, N'Smart TV Xiaomi 4K 43 inch', N'Xiaomi', N'L43MA-AUSEA', 1, 43, N'4K UHD', N'Google TV', 24, 5990000, 7290000, 25, 0),
    (14, N'Smart TV Panasonic 4K 55 inch', N'Panasonic', N'TH-55MX650V', 1, 55, N'4K UHD', N'Google TV', 24, 11490000, 13490000, 6, 0),
    (15, N'Casper Smart TV Full HD 43 inch', N'Casper', N'43FGK610', 1, 43, N'Full HD', N'Android TV', 24, 4690000, 5990000, 30, 0),
    (16, N'Toshiba QLED 4K 65 inch', N'Toshiba', N'65M550NP', 3, 65, N'4K QLED', N'Google TV', 24, 12990000, 15990000, 5, 0),
    (17, N'Samsung Neo QLED 4K 75 inch', N'Samsung', N'QA75QN85D', 3, 75, N'4K Neo QLED', N'Tizen', 24, 42990000, 49990000, 3, 1),
    (18, N'LG OLED evo G4 77 inch', N'LG', N'OLED77G4PSA', 2, 77, N'4K OLED', N'webOS', 36, 129990000, 149990000, 1, 1),
    (19, N'Sony Bravia 8 OLED 55 inch', N'Sony', N'K-55XR80', 2, 55, N'4K OLED', N'Google TV', 24, 36990000, 42990000, 2, 1),
    (20, N'Hisense QLED 4K 55 inch', N'Hisense', N'55E7NQ', 3, 55, N'4K QLED', N'VIDAA', 24, 9990000, 11990000, 12, 0),
    (21, N'Smart TV Samsung Crystal UHD 4K 75 inch', N'Samsung', N'UA75DU8000', 1, 75, N'4K UHD', N'Tizen', 24, 21990000, 25990000, 4, 0),
    (22, N'LG QNED 4K 65 inch', N'LG', N'65QNED82TSA', 3, 65, N'4K QNED', N'webOS', 24, 17990000, 21490000, 8, 0),
    (23, N'Philips Ambilight 4K 55 inch', N'Philips', N'55PUS8509', 1, 55, N'4K UHD', N'Google TV', 24, 12490000, 14990000, 0, 0),
    (24, N'TCL Mini LED 4K 85 inch', N'TCL', N'85C855', 3, 85, N'4K Mini LED', N'Google TV', 24, 38990000, 45990000, 2, 0);
SET IDENTITY_INSERT dbo.Products OFF;
GO

-- Ảnh sản phẩm (bảng ProductImages). Ảnh minh họa dạng SVG, bạn có thể tải ảnh thật lên từ trang Quản trị.
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (1, N'ua55du8000.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SAMSUNG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">UA55DU8000</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (2, N'oled55b4psa.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#a78bfa"/><stop offset="1" stop-color="#1e1b4b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">LG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - OLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">OLED55B4PSA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (3, N'kd-65x75k.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SONY</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">KD-65X75K</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (4, N'qa65q60d.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SAMSUNG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">QA65Q60D</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (5, N'l55ma-apsea.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">XIAOMI</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">L55MA-APSEA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (6, N'50p755.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">TCL</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">50 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">50P755</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (7, N'oled65c4psa.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#a78bfa"/><stop offset="1" stop-color="#1e1b4b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">LG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - OLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">OLED65C4PSA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (8, N'k-55s30.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SONY</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">K-55S30</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (9, N'ua43du7700.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SAMSUNG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">43 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">UA43DU7700</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (10, N'50ur8050psb.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">LG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">50 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">50UR8050PSB</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (11, N'k-50s25b.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SONY</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">50 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">K-50S25B</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (12, N'65c655.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">TCL</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">65C655</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (13, N'l43ma-ausea.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">XIAOMI</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">43 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">L43MA-AUSEA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (14, N'th-55mx650v.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">PANASONIC</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">TH-55MX650V</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (15, N'43fgk610.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">CASPER</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">43 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">43FGK610</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (16, N'65m550np.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">TOSHIBA</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">65M550NP</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (17, N'qa75qn85d.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SAMSUNG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">75 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">QA75QN85D</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (18, N'oled77g4psa.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#a78bfa"/><stop offset="1" stop-color="#1e1b4b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">LG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">77 inch - OLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">OLED77G4PSA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (19, N'k-55xr80.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#a78bfa"/><stop offset="1" stop-color="#1e1b4b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SONY</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - OLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">K-55XR80</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (20, N'55e7nq.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">HISENSE</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">55E7NQ</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (21, N'ua75du8000.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">SAMSUNG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">75 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">UA75DU8000</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (22, N'65qned82tsa.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">LG</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">65 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">65QNED82TSA</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (23, N'55pus8509.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#60a5fa"/><stop offset="1" stop-color="#1e3a8a"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">PHILIPS</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">55 inch - LED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">55PUS8509</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
INSERT INTO dbo.ProductImages (ProductId, FileName, ContentType, ImageData, SortOrder) VALUES (24, N'85c855.svg', 'image/svg+xml', CAST('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 260"><defs><linearGradient id="s" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#34d399"/><stop offset="1" stop-color="#064e3b"/></linearGradient></defs><rect width="400" height="260" fill="#f1f5f9"/><rect x="40" y="30" width="320" height="176" rx="10" fill="#111827"/><rect x="50" y="40" width="300" height="156" rx="4" fill="url(#s)"/><path d="M60 52L150 52L96 130Z" fill="#ffffff" fill-opacity=".14"/><text x="200" y="112" font-family="Arial,sans-serif" font-size="30" font-weight="700" fill="#ffffff" text-anchor="middle">TCL</text><text x="200" y="146" font-family="Arial,sans-serif" font-size="20" fill="#e0f2fe" text-anchor="middle">85 inch - QLED</text><text x="200" y="174" font-family="Arial,sans-serif" font-size="14" fill="#bfdbfe" text-anchor="middle">85C855</text><rect x="170" y="206" width="60" height="10" fill="#374151"/><rect x="135" y="216" width="130" height="9" rx="3" fill="#4b5563"/></svg>' AS VARBINARY(MAX)), 0);
GO

SET IDENTITY_INSERT dbo.Suppliers ON;
INSERT INTO dbo.Suppliers (Id, Name, ContactPerson, Phone, Email, Address) VALUES
    (1, N'Công ty TNHH Phân phối Điện tử Hà Thành', N'Nguyễn Hải Đăng', '02438123456', N'hathanh@example.com', N'25 Nguyễn Trãi, Thanh Xuân, Hà Nội'),
    (2, N'Công ty CP Thương mại Việt Tiến', N'Trần Quang Huy', '02439876543', N'viettien@example.com', N'112 Giải Phóng, Hai Bà Trưng, Hà Nội'),
    (3, N'Công ty TNHH Điện máy Bảo An', N'Lê Thị Bảo An', '02437654321', N'baoan@example.com', N'8 Láng Hạ, Đống Đa, Hà Nội'),
    (4, N'Tổng kho Điện tử Miền Bắc', N'Phạm Văn Cường', '02435551234', N'tongkhomb@example.com', N'KCN Quang Minh, Mê Linh, Hà Nội'),
    (5, N'Công ty TNHH Nhập khẩu Thiết bị Số Á Châu', N'Hoàng Minh Tuấn', '02836112233', N'achau@example.com', N'45 Lê Lợi, Quận 1, TP. Hồ Chí Minh'),
    (6, N'Đại lý cấp 1 Minh Phát', N'Vũ Thị Minh', '02466778899', N'minhphat@example.com', N'60 Trường Chinh, Đống Đa, Hà Nội');
SET IDENTITY_INSERT dbo.Suppliers OFF;
GO

SET IDENTITY_INSERT dbo.Employees ON;
INSERT INTO dbo.Employees (Id, FullName, Phone, Email, Position, HireDate) VALUES
    (1, N'Nguyễn Thị Lan', '0902111222', N'lan.nt@example.com', N'Trưởng nhóm bán hàng', '2022-03-01'),
    (2, N'Trần Văn Hùng', '0902333444', N'hung.tv@example.com', N'Nhân viên tư vấn', '2023-06-15'),
    (3, N'Lê Thị Mai', '0902555666', N'mai.lt@example.com', N'Nhân viên tư vấn', '2023-09-04'),
    (4, N'Phạm Quốc Bảo', '0902777888', N'bao.pq@example.com', N'Nhân viên bán hàng', '2024-02-19'),
    (5, N'Đỗ Thu Trang', '0902999000', N'trang.dt@example.com', N'Nhân viên bán hàng', '2024-08-12'),
    (6, N'Vũ Minh Khoa', '0903111333', N'khoa.vm@example.com', N'Thu ngân kiêm tư vấn', '2025-01-06');
SET IDENTITY_INSERT dbo.Employees OFF;
GO

SET IDENTITY_INSERT dbo.Customers ON;
INSERT INTO dbo.Customers (Id, FullName, Email, Phone) VALUES
    (1, N'Nguyễn Văn An', N'an@example.com', '0912345678'),
    (2, N'Trần Minh Anh', N'minhanh@example.com', '0987654321'),
    (3, N'Lê Hoàng Nam', N'nam@example.com', '0909123456'),
    (4, N'Phạm Thu Hà', N'ha@example.com', '0933445566'),
    (5, N'Hoàng Thị Thu', N'thu.ht@example.com', '0911222333'),
    (6, N'Đặng Quốc Việt', N'viet.dq@example.com', '0922333444'),
    (7, N'Bùi Ngọc Ánh', N'anh.bn@example.com', '0944555666'),
    (8, N'Vũ Đức Thắng', N'thang.vd@example.com', '0955666777'),
    (9, N'Ngô Thị Hạnh', N'hanh.nt@example.com', '0966777888'),
    (10, N'Đinh Công Sơn', N'son.dc@example.com', '0977888999'),
    (11, N'Dương Thanh Tâm', N'tam.dt@example.com', '0988999000'),
    (12, N'Lý Gia Bảo', N'bao.lg@example.com', '0999000111');
SET IDENTITY_INSERT dbo.Customers OFF;
GO

SET IDENTITY_INSERT dbo.DiscountCodes ON;
INSERT INTO dbo.DiscountCodes (Id, Code, DiscountPercent, ProductId, StartDate, EndDate, UsageLimit, UsedCount, IsActive) VALUES
    (1, N'TVHOUSE20', 20, NULL, '2026-10-01', '2026-12-31', NULL, 10, 1),
    (2, N'GIAM30', 30, NULL, '2026-11-01', '2026-11-30', 50, 0, 1),
    (3, N'SAMSUNG40', 40, 4, '2026-10-15', '2026-11-30', 20, 0, 1),
    (4, N'OLED25', 25, 2, '2026-10-01', '2026-12-15', NULL, 0, 1),
    (5, N'FLASH50', 50, 6, '2026-11-05', '2026-11-07', 10, 1, 1),
    (6, N'HETHAN20', 20, NULL, '2026-09-01', '2026-09-30', NULL, 0, 1),
    (7, N'TET2027-35', 35, NULL, '2026-12-20', '2027-02-15', NULL, 0, 1),
    (8, N'NGUNG30', 30, NULL, '2026-10-01', '2026-12-31', NULL, 0, 0);
SET IDENTITY_INSERT dbo.DiscountCodes OFF;
GO

-- Đơn hàng DH0101 .. DH0132 (Id 101..132), đơn mới sẽ tiếp tục sau đó
SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders (Id, CustomerId, EmployeeId, OrderDate, SubtotalAmount, DiscountCodeId, DiscountAmount, TotalAmount, PaymentMethod, Status) VALUES
    (101, 1, 6, '2026-10-20T15:20:00', 8990000, 1, 1798000, 7192000, N'Thẻ', 'hoan_thanh'),
    (102, 2, 4, '2026-10-21T01:52:00', 11490000, 1, 2298000, 9192000, N'Ví điện tử', 'hoan_thanh'),
    (103, 10, 4, '2026-10-21T13:47:00', 42990000, 1, 8598000, 34392000, N'Tiền mặt', 'hoan_thanh'),
    (104, 9, 6, '2026-10-21T20:43:00', 8990000, NULL, 0, 8990000, N'Tiền mặt', 'hoan_thanh'),
    (105, 8, 2, '2026-10-22T04:40:00', 11490000, NULL, 0, 11490000, N'Tiền mặt', 'hoan_thanh'),
    (106, 9, 3, '2026-10-22T15:19:00', 4690000, NULL, 0, 4690000, N'Ví điện tử', 'da_huy'),
    (107, 12, 4, '2026-10-23T01:45:00', 8990000, NULL, 0, 8990000, N'Ví điện tử', 'hoan_thanh'),
    (108, 11, 5, '2026-10-23T14:21:00', 8990000, 1, 1798000, 7192000, N'Ví điện tử', 'hoan_thanh'),
    (109, 5, 6, '2026-10-24T01:14:00', 12990000, 1, 2598000, 10392000, N'Ví điện tử', 'hoan_thanh'),
    (110, NULL, 5, '2026-10-24T13:03:00', 12990000, NULL, 0, 12990000, N'Tiền mặt', 'hoan_thanh'),
    (111, 2, 4, '2026-10-25T01:34:00', 11990000, NULL, 0, 11990000, N'Thẻ', 'hoan_thanh'),
    (112, 8, 1, '2026-10-25T14:23:00', 7990000, NULL, 0, 7990000, N'Thẻ', 'hoan_thanh'),
    (113, 3, 4, '2026-10-26T00:51:00', 13990000, 1, 2798000, 11192000, N'Tiền mặt', 'hoan_thanh'),
    (114, 10, 2, '2026-10-26T07:51:00', 41680000, NULL, 0, 41680000, N'Chuyển khoản', 'hoan_thanh'),
    (115, 11, 6, '2026-10-26T17:56:00', 152980000, NULL, 0, 152980000, N'Thẻ', 'hoan_thanh'),
    (116, 8, 6, '2026-10-27T00:14:00', 29980000, NULL, 0, 29980000, N'Chuyển khoản', 'hoan_thanh'),
    (117, 10, 6, '2026-10-27T12:20:00', 12990000, NULL, 0, 12990000, N'Thẻ', 'hoan_thanh'),
    (118, 10, 3, '2026-10-27T21:20:00', 25980000, NULL, 0, 25980000, N'Ví điện tử', 'da_huy'),
    (119, 2, 4, '2026-10-28T10:17:00', 8990000, 1, 1798000, 7192000, N'Tiền mặt', 'hoan_thanh'),
    (120, 1, 5, '2026-10-28T21:48:00', 35980000, 1, 7196000, 28784000, N'Ví điện tử', 'hoan_thanh'),
    (121, 8, 4, '2026-10-29T09:08:00', 8990000, NULL, 0, 8990000, N'Chuyển khoản', 'hoan_thanh'),
    (122, 6, 4, '2026-10-29T16:33:00', 82980000, NULL, 0, 82980000, N'Chuyển khoản', 'hoan_thanh'),
    (123, 1, 2, '2026-10-30T00:55:00', 16990000, NULL, 0, 16990000, N'Chuyển khoản', 'hoan_thanh'),
    (124, NULL, 6, '2026-10-30T09:46:00', 152980000, NULL, 0, 152980000, N'Chuyển khoản', 'hoan_thanh'),
    (125, 3, 6, '2026-10-30T19:33:00', 17990000, NULL, 0, 17990000, N'Chuyển khoản', 'hoan_thanh'),
    (126, 3, 2, '2026-10-31T06:13:00', 42990000, NULL, 0, 42990000, N'Chuyển khoản', 'hoan_thanh'),
    (127, 2, 4, '2026-10-31T12:59:00', 11490000, NULL, 0, 11490000, N'Thẻ', 'hoan_thanh'),
    (128, 5, 4, '2026-10-31T21:04:00', 15480000, NULL, 0, 15480000, N'Chuyển khoản', 'hoan_thanh'),
    (129, 9, 5, '2026-11-04T15:30:00', 12990000, NULL, 0, 12990000, N'Thẻ', 'dang_giao'),
    (130, 8, 6, '2026-11-05T09:10:00', 39990000, 1, 7998000, 31992000, N'Thẻ', 'dang_giao'),
    (131, 6, 2, '2026-11-05T10:40:00', 17990000, 1, 3598000, 14392000, N'Thẻ', 'da_xac_nhan'),
    (132, 9, 6, '2026-11-05T11:25:00', 7990000, 5, 3995000, 3995000, N'Chuyển khoản', 'cho_xac_nhan');
SET IDENTITY_INSERT dbo.Orders OFF;
GO

INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES
    (101, 10, 1, 8990000),
    (102, 14, 1, 11490000),
    (103, 17, 1, 42990000),
    (104, 5, 1, 8990000),
    (105, 14, 1, 11490000),
    (106, 15, 1, 4690000),
    (107, 10, 1, 8990000),
    (108, 10, 1, 8990000),
    (109, 16, 1, 12990000),
    (110, 16, 1, 12990000),
    (111, 11, 1, 11990000),
    (112, 6, 1, 7990000),
    (113, 12, 1, 13990000),
    (114, 15, 1, 4690000),
    (114, 19, 1, 36990000),
    (115, 2, 1, 22990000),
    (115, 18, 1, 129990000),
    (116, 11, 1, 11990000),
    (116, 22, 1, 17990000),
    (117, 16, 1, 12990000),
    (118, 22, 1, 17990000),
    (118, 6, 1, 7990000),
    (119, 5, 1, 8990000),
    (120, 12, 1, 13990000),
    (120, 21, 1, 21990000),
    (121, 10, 1, 8990000),
    (122, 17, 1, 42990000),
    (122, 7, 1, 39990000),
    (123, 3, 1, 16990000),
    (124, 18, 1, 129990000),
    (124, 2, 1, 22990000),
    (125, 22, 1, 17990000),
    (126, 17, 1, 42990000),
    (127, 14, 1, 11490000),
    (128, 6, 1, 7990000),
    (128, 9, 1, 7490000),
    (129, 16, 1, 12990000),
    (130, 7, 1, 39990000),
    (131, 22, 1, 17990000),
    (132, 6, 1, 7990000);
GO

-- Đơn đã hoàn thành thì có hóa đơn
INSERT INTO dbo.Invoices (OrderId, InvoiceDate, TotalAmount) VALUES
    (101, '2026-10-20T15:25:00', 7192000),
    (102, '2026-10-21T01:57:00', 9192000),
    (103, '2026-10-21T13:52:00', 34392000),
    (104, '2026-10-21T20:48:00', 8990000),
    (105, '2026-10-22T04:45:00', 11490000),
    (107, '2026-10-23T01:50:00', 8990000),
    (108, '2026-10-23T14:26:00', 7192000),
    (109, '2026-10-24T01:19:00', 10392000),
    (110, '2026-10-24T13:08:00', 12990000),
    (111, '2026-10-25T01:39:00', 11990000),
    (112, '2026-10-25T14:28:00', 7990000),
    (113, '2026-10-26T00:56:00', 11192000),
    (114, '2026-10-26T07:56:00', 41680000),
    (115, '2026-10-26T18:01:00', 152980000),
    (116, '2026-10-27T00:19:00', 29980000),
    (117, '2026-10-27T12:25:00', 12990000),
    (119, '2026-10-28T10:22:00', 7192000),
    (120, '2026-10-28T21:53:00', 28784000),
    (121, '2026-10-29T09:13:00', 8990000),
    (122, '2026-10-29T16:38:00', 82980000),
    (123, '2026-10-30T01:00:00', 16990000),
    (124, '2026-10-30T09:51:00', 152980000),
    (125, '2026-10-30T19:38:00', 17990000),
    (126, '2026-10-31T06:18:00', 42990000),
    (127, '2026-10-31T13:04:00', 11490000),
    (128, '2026-10-31T21:09:00', 15480000);
GO

INSERT INTO dbo.ImportReceipts (ImportDate, SupplierId, ProductId, Quantity, UnitPrice) VALUES
    ('2026-10-01T13:18:00', 1, 4, 13, 14240000),
    ('2026-10-02T12:48:00', 3, 19, 11, 27740000),
    ('2026-10-03T14:05:00', 3, 19, 13, 27740000),
    ('2026-10-04T10:36:00', 4, 5, 8, 6740000),
    ('2026-10-05T09:13:00', 5, 14, 10, 8610000),
    ('2026-10-06T14:21:00', 2, 2, 16, 17240000),
    ('2026-10-07T12:50:00', 4, 6, 8, 5990000),
    ('2026-10-08T11:29:00', 1, 9, 7, 5610000),
    ('2026-10-09T10:46:00', 3, 11, 10, 8990000),
    ('2026-10-10T12:25:00', 3, 3, 5, 12740000),
    ('2026-10-11T08:33:00', 1, 9, 18, 5610000),
    ('2026-10-12T08:49:00', 1, 4, 12, 14240000),
    ('2026-10-13T14:05:00', 1, 4, 8, 14240000),
    ('2026-10-14T11:19:00', 3, 19, 16, 27740000),
    ('2026-10-15T11:50:00', 6, 15, 6, 3510000),
    ('2026-10-16T12:59:00', 4, 20, 11, 7490000),
    ('2026-10-17T13:53:00', 5, 14, 8, 8610000),
    ('2026-10-18T11:48:00', 1, 17, 13, 32240000);
GO

/* ---------------------------------------------------------------------
   4. KIỂM TRA NHANH
   --------------------------------------------------------------------- */
SELECT 'Categories' AS [Bang], COUNT(*) AS [SoDong] FROM dbo.Categories
UNION ALL SELECT 'Products',        COUNT(*) FROM dbo.Products
UNION ALL SELECT 'ProductImages',   COUNT(*) FROM dbo.ProductImages
UNION ALL SELECT 'Suppliers',       COUNT(*) FROM dbo.Suppliers
UNION ALL SELECT 'Employees',       COUNT(*) FROM dbo.Employees
UNION ALL SELECT 'Customers',       COUNT(*) FROM dbo.Customers
UNION ALL SELECT 'DiscountCodes',   COUNT(*) FROM dbo.DiscountCodes
UNION ALL SELECT 'Orders',          COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderItems',      COUNT(*) FROM dbo.OrderItems
UNION ALL SELECT 'Invoices',        COUNT(*) FROM dbo.Invoices
UNION ALL SELECT 'ImportReceipts',  COUNT(*) FROM dbo.ImportReceipts
UNION ALL SELECT 'AdminUsers',      COUNT(*) FROM dbo.AdminUsers;
GO
