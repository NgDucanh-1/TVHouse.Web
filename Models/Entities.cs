using System.ComponentModel.DataAnnotations.Schema;

namespace TVHouse.Web.Models;

/// <summary>
/// Bảng Categories - danh mục sản phẩm (Tivi LED / OLED / QLED).
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Product> Products { get; set; } = new();
}

/// <summary>
/// Bảng Products - thông tin 1 mẫu tivi: tên, hãng, model, kích thước, giá bán/giá gốc, tồn kho, nổi bật.
/// IsActive = false nghĩa là đã "xóa" (ẩn) khỏi cửa hàng.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int ScreenSize { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public int WarrantyMonths { get; set; } = 24;
    public decimal Price { get; set; }
    public decimal OldPrice { get; set; }
    public int Stock { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Các ảnh của sản phẩm (bảng ProductImages). Đừng Include khi liệt kê - ảnh nặng.</summary>
    public List<ProductImage> Images { get; set; } = new();

    /// <summary>Id ảnh chính (không lưu DB) - được nạp bởi AttachMainImagesAsync.</summary>
    [NotMapped]
    public int? MainImageId { get; set; }
}

/// <summary>Ảnh sản phẩm lưu trong SQL Server (bảng riêng, nối với Products).</summary>
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Bảng AdminUsers - tài khoản đăng nhập trang Quản trị (mật khẩu lưu dạng băm, có bộ đếm nhập sai để khóa tạm).
/// </summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Bảng Suppliers - nhà cung cấp hàng; mỗi phiếu nhập hàng gắn với 1 nhà cung cấp.
/// </summary>
public class Supplier
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;   // cột tính toán (NCC001)
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Bảng Employees - nhân viên bán hàng; được chọn trên hóa đơn để biết ai đã tư vấn cho khách.
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;   // cột tính toán (NV001)
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Position { get; set; } = string.Empty;
    public DateTime HireDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Bảng Customers - khách hàng; tự tạo hoặc cập nhật khi xuất hóa đơn (trùng số điện thoại thì dùng lại).
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<Order> Orders { get; set; } = new();
}

/// <summary>
/// Bảng DiscountCodes - mã giảm giá từ 20% đến 50%, áp dụng cho 1 sản phẩm hoặc toàn bộ, có ngày hiệu lực và giới hạn lượt dùng.
/// </summary>
public class DiscountCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int DiscountPercent { get; set; } = 20;              // 20 - 50
    public int? ProductId { get; set; }                         // null = áp dụng mọi sản phẩm
    public Product? Product { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string StatusLabel(DateTime today)
    {
        if (!IsActive) return "Đã tắt";
        if (today.Date < StartDate.Date) return "Chưa bắt đầu";
        if (today.Date > EndDate.Date) return "Hết hạn";
        if (UsageLimit.HasValue && UsedCount >= UsageLimit.Value) return "Hết lượt";
        return "Đang áp dụng";
    }

    public string StatusCss(DateTime today) => StatusLabel(today) == "Đang áp dụng" ? "good" : "bad";
}

/// <summary>
/// Bảng Orders - đơn hàng: khách, nhân viên tư vấn, tạm tính, mã giảm giá, tiền giảm, tổng thanh toán, phương thức thanh toán, trạng thái.
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Mã đơn (DH0128...) - cột tính toán do SQL Server sinh.</summary>
    public string OrderCode { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Nhân viên tư vấn bán hàng.</summary>
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;
    public decimal SubtotalAmount { get; set; }
    public int? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Tiền mặt";
    public string Status { get; set; } = OrderStatus.Pending;
    public List<OrderItem> Items { get; set; } = new();
    public Invoice? Invoice { get; set; }

    public string CustomerName => Customer?.FullName ?? "Khách hàng tại quầy";
    public string EmployeeName => Employee?.FullName ?? "—";
}

/// <summary>
/// Bảng OrderItems - từng dòng sản phẩm trong đơn (lưu giá tại thời điểm bán, nên đổi giá sau này không ảnh hưởng đơn cũ).
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Bảng Invoices - hóa đơn bán hàng, mỗi đơn có tối đa 1 hóa đơn.
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Bảng ImportReceipts - phiếu nhập hàng: nhà cung cấp, sản phẩm, số lượng, đơn giá nhập.
/// </summary>
public class ImportReceipt
{
    public int Id { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateTime ImportDate { get; set; } = DateTime.Now;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>
/// Danh sách phương thức thanh toán cho phép chọn khi xuất hóa đơn.
/// </summary>
public static class PaymentMethods
{
    public static readonly string[] All = { "Tiền mặt", "Chuyển khoản", "Thẻ", "Ví điện tử" };
}

/// <summary>
/// Các trạng thái đơn hàng (lưu trong SQL dạng chữ không dấu) và tên hiển thị tiếng Việt tương ứng.
/// </summary>
public static class OrderStatus
{
    public const string Pending = "cho_xac_nhan";
    public const string Confirmed = "da_xac_nhan";
    public const string Shipping = "dang_giao";
    public const string Completed = "hoan_thanh";
    public const string Cancelled = "da_huy";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        { Pending, "Chờ xác nhận" },
        { Confirmed, "Đã xác nhận" },
        { Shipping, "Đang giao" },
        { Completed, "Hoàn thành" },
        { Cancelled, "Đã hủy" }
    };

    public static string Label(string status) =>
        Labels.TryGetValue(status, out var label) ? label : status;

    public static string CssClass(string status) =>
        status == Completed ? "good" : status == Cancelled ? "bad" : "";
}
