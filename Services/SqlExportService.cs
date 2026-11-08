using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;

namespace TVHouse.Web.Services;

/// <summary>
/// Xuất toàn bộ dữ liệu hiện tại trong SQL Server thành 1 file .sql hoàn chỉnh:
/// phần cấu trúc bảng (lấy từ Database/TVHouse.sql, phần trên dòng "BEGIN SEED DATA")
/// + các câu lệnh INSERT cho dữ liệu đang có (kể cả ảnh sản phẩm).
/// </summary>
public class SqlExportService
{
    private const string SeedMarker = "/* ===== BEGIN SEED DATA ===== */";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SqlExportService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>
    /// Tạo nội dung file .sql: đọc cấu trúc bảng, sau đó lần lượt ghi dữ liệu từng bảng theo thứ tự khóa ngoại (bảng cha trước, bảng con sau).
    /// </summary>
    public async Task<string> BuildScriptAsync()
    {
        var path = Path.Combine(_env.ContentRootPath, "Database", "TVHouse.sql");
        if (!File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy file Database/TVHouse.sql để lấy cấu trúc bảng.", path);

        var template = await File.ReadAllTextAsync(path);
        var idx = template.IndexOf(SeedMarker, StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidOperationException("File TVHouse.sql thiếu dòng đánh dấu BEGIN SEED DATA.");

        var sb = new StringBuilder();
        sb.Append(template, 0, idx);
        sb.AppendLine(SeedMarker);
        sb.AppendLine();
        sb.AppendLine($"/* Dữ liệu được xuất từ website lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss} */");
        sb.AppendLine();

        foreach (var u in await _db.AdminUsers.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "AdminUsers", true, "Id, Username, PasswordHash, DisplayName, IsActive, FailedAttempts, LockoutEnd, CreatedAt",
                $"{u.Id}, {S(u.Username)}, {V(u.PasswordHash)}, {S(u.DisplayName)}, {Bit(u.IsActive)}, {u.FailedAttempts}, {(u.LockoutEnd.HasValue ? Dt(u.LockoutEnd.Value) : "NULL")}, {Dt(u.CreatedAt)}");
        End(sb, "AdminUsers");

        foreach (var c in await _db.Categories.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Categories", true, "Id, Name", $"{c.Id}, {S(c.Name)}");
        End(sb, "Categories");

        foreach (var p in await _db.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Products", true,
                "Id, Name, Brand, Model, CategoryId, ScreenSize, Resolution, OperatingSystem, WarrantyMonths, Price, OldPrice, Stock, IsFeatured, IsActive, CreatedAt",
                $"{p.Id}, {S(p.Name)}, {S(p.Brand)}, {S(p.Model)}, {p.CategoryId}, {p.ScreenSize}, {S(p.Resolution)}, {S(p.OperatingSystem)}, {p.WarrantyMonths}, {M(p.Price)}, {M(p.OldPrice)}, {p.Stock}, {Bit(p.IsFeatured)}, {Bit(p.IsActive)}, {Dt(p.CreatedAt)}");
        End(sb, "Products");

        foreach (var i in await _db.ProductImages.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "ProductImages", true, "Id, ProductId, FileName, ContentType, ImageData, SortOrder, CreatedAt",
                $"{i.Id}, {i.ProductId}, {S(i.FileName)}, {V(i.ContentType)}, 0x{Convert.ToHexString(i.ImageData)}, {i.SortOrder}, {Dt(i.CreatedAt)}");
        End(sb, "ProductImages");

        foreach (var s in await _db.Suppliers.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Suppliers", true, "Id, Name, ContactPerson, Phone, Email, Address, IsActive, CreatedAt",
                $"{s.Id}, {S(s.Name)}, {S(s.ContactPerson)}, {V(s.Phone)}, {S(s.Email)}, {S(s.Address)}, {Bit(s.IsActive)}, {Dt(s.CreatedAt)}");
        End(sb, "Suppliers");

        foreach (var e in await _db.Employees.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Employees", true, "Id, FullName, Phone, Email, Position, HireDate, IsActive",
                $"{e.Id}, {S(e.FullName)}, {V(e.Phone)}, {S(e.Email)}, {S(e.Position)}, {D(e.HireDate)}, {Bit(e.IsActive)}");
        End(sb, "Employees");

        foreach (var c in await _db.Customers.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Customers", true, "Id, FullName, Email, Phone",
                $"{c.Id}, {S(c.FullName)}, {S(c.Email)}, {V(c.Phone)}");
        End(sb, "Customers");

        foreach (var d in await _db.DiscountCodes.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "DiscountCodes", true,
                "Id, Code, DiscountPercent, ProductId, StartDate, EndDate, UsageLimit, UsedCount, IsActive, CreatedAt",
                $"{d.Id}, {S(d.Code)}, {d.DiscountPercent}, {I(d.ProductId)}, {D(d.StartDate)}, {D(d.EndDate)}, {I(d.UsageLimit)}, {d.UsedCount}, {Bit(d.IsActive)}, {Dt(d.CreatedAt)}");
        End(sb, "DiscountCodes");

        foreach (var o in await _db.Orders.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Orders", true,
                "Id, CustomerId, EmployeeId, OrderDate, SubtotalAmount, DiscountCodeId, DiscountAmount, TotalAmount, PaymentMethod, Status",
                $"{o.Id}, {I(o.CustomerId)}, {I(o.EmployeeId)}, {Dt(o.OrderDate)}, {M(o.SubtotalAmount)}, {I(o.DiscountCodeId)}, {M(o.DiscountAmount)}, {M(o.TotalAmount)}, {S(o.PaymentMethod)}, {V(o.Status)}");
        End(sb, "Orders");

        foreach (var i in await _db.OrderItems.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "OrderItems", true, "Id, OrderId, ProductId, Quantity, UnitPrice",
                $"{i.Id}, {i.OrderId}, {i.ProductId}, {i.Quantity}, {M(i.UnitPrice)}");
        End(sb, "OrderItems");

        foreach (var i in await _db.Invoices.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "Invoices", true, "Id, OrderId, InvoiceDate, TotalAmount",
                $"{i.Id}, {i.OrderId}, {Dt(i.InvoiceDate)}, {M(i.TotalAmount)}");
        End(sb, "Invoices");

        foreach (var r in await _db.ImportReceipts.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            Row(sb, "ImportReceipts", true, "Id, ImportDate, SupplierId, ProductId, Quantity, UnitPrice",
                $"{r.Id}, {Dt(r.ImportDate)}, {r.SupplierId}, {r.ProductId}, {r.Quantity}, {M(r.UnitPrice)}");
        End(sb, "ImportReceipts");

        return sb.ToString();
    }

    // ---- ghi từng bảng: bật IDENTITY_INSERT ở dòng đầu tiên của bảng ----
    private readonly HashSet<string> _started = new();

    /// <summary>
    /// Ghi 1 câu INSERT cho 1 dòng dữ liệu; dòng đầu tiên của mỗi bảng sẽ bật IDENTITY_INSERT để giữ nguyên Id cũ.
    /// </summary>
    private void Row(StringBuilder sb, string table, bool identity, string columns, string values)
    {
        if (identity && _started.Add(table))
            sb.AppendLine($"SET IDENTITY_INSERT dbo.{table} ON;");
        sb.AppendLine($"INSERT INTO dbo.{table} ({columns}) VALUES ({values});");
    }

    /// <summary>
    /// Kết thúc 1 bảng: tắt IDENTITY_INSERT và thêm lệnh GO.
    /// </summary>
    private void End(StringBuilder sb, string table)
    {
        if (_started.Contains(table))
            sb.AppendLine($"SET IDENTITY_INSERT dbo.{table} OFF;");
        sb.AppendLine("GO");
        sb.AppendLine();
    }

    // ---- định dạng giá trị SQL ----
    /// <summary>
    /// Định dạng chuỗi Unicode cho SQL (N'...'), nhân đôi dấu nháy đơn; null thành NULL.
    /// </summary>
    private static string S(string? v) => v == null ? "NULL" : "N'" + v.Replace("'", "''") + "'";
    /// <summary>
    /// Định dạng chuỗi không Unicode cho SQL ('...'); null thành NULL.
    /// </summary>
    private static string V(string? v) => v == null ? "NULL" : "'" + v.Replace("'", "''") + "'";
    /// <summary>
    /// Định dạng số nguyên có thể rỗng (null thành NULL).
    /// </summary>
    private static string I(int? v) => v.HasValue ? v.Value.ToString(Inv) : "NULL";
    /// <summary>
    /// Định dạng số tiền (không có phần thập phân).
    /// </summary>
    private static string M(decimal v) => v.ToString("0", Inv);
    /// <summary>
    /// Đổi true/false thành 1/0 cho cột BIT.
    /// </summary>
    private static string Bit(bool v) => v ? "1" : "0";
    /// <summary>
    /// Định dạng ngày giờ cho SQL: 'yyyy-MM-ddTHH:mm:ss'.
    /// </summary>
    private static string Dt(DateTime v) => "'" + v.ToString("yyyy-MM-ddTHH:mm:ss", Inv) + "'";
    /// <summary>
    /// Định dạng ngày cho SQL: 'yyyy-MM-dd'.
    /// </summary>
    private static string D(DateTime v) => "'" + v.ToString("yyyy-MM-dd", Inv) + "'";
}
