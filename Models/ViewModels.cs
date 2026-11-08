using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TVHouse.Web.Models;

// ---------- Cửa hàng ----------
/// <summary>
/// Dữ liệu cho trang chủ cửa hàng: danh sách sản phẩm, danh mục, bộ lọc đang chọn.
/// </summary>
public class ShopViewModel
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string ActiveCategory { get; set; } = "";
    public string? Query { get; set; }
    public string Sort { get; set; } = "featured";
}

// ---------- Giỏ hàng / Xuất hóa đơn ----------
/// <summary>
/// 1 dòng trong giỏ hàng lưu ở Session: mã sản phẩm + số lượng.
/// </summary>
public class CartLine
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// 1 dòng giỏ hàng để hiển thị: sản phẩm đầy đủ + số lượng + thành tiền.
/// </summary>
public class CartLineViewModel
{
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal LineTotal => Product.Price * Quantity;
}

/// <summary>Trang giỏ hàng + form thông tin khách mua tại quầy để xuất hóa đơn.</summary>
public class CheckoutViewModel
{
    // --- hiển thị (do server tính, không nhận từ form) ---
    public List<CartLineViewModel> Lines { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();
    public decimal Subtotal => Lines.Sum(l => l.LineTotal);
    public decimal DiscountAmount { get; set; }
    public decimal Total => Subtotal - DiscountAmount;
    public bool DiscountValid { get; set; }
    public string? DiscountMessage { get; set; }

    // --- dữ liệu người dùng nhập ---
    [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string CustomerName { get; set; } = string.Empty;

    [RegularExpression(@"^[0-9+\-\.\s]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
    public string? Email { get; set; }

    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
    public string PaymentMethod { get; set; } = "Tiền mặt";

    [StringLength(30, ErrorMessage = "Mã giảm giá tối đa 30 ký tự")]
    public string? DiscountCode { get; set; }
}

// ---------- Quản trị ----------
/// <summary>
/// 4 số thống kê hiển thị đầu mọi trang quản trị.
/// </summary>
public class AdminStats
{
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
    public int LowStockCount { get; set; }
}

/// <summary>
/// Dữ liệu trang Tổng quan quản trị: đơn cần xử lý và hàng sắp hết.
/// </summary>
public class AdminOverviewViewModel
{
    public List<Order> PendingOrders { get; set; } = new();
    public List<Product> LowStock { get; set; } = new();
}

/// <summary>
/// 1 dòng danh sách nhà cung cấp kèm số phiếu nhập.
/// </summary>
public class SupplierRow
{
    public Supplier Supplier { get; set; } = null!;
    public int ReceiptCount { get; set; }
}

/// <summary>
/// 1 dòng danh sách nhân viên kèm số hóa đơn và doanh thu đã tư vấn.
/// </summary>
public class EmployeeRow
{
    public Employee Employee { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Dữ liệu trang báo cáo: danh sách đơn hoàn thành và các số tổng hợp.
/// </summary>
public class ReportViewModel
{
    public List<Order> CompletedOrders { get; set; } = new();
    public decimal Revenue => CompletedOrders.Sum(o => o.TotalAmount);
    public decimal Discounts => CompletedOrders.Sum(o => o.DiscountAmount);
    public decimal Average => CompletedOrders.Count == 0 ? 0 : Math.Round(Revenue / CompletedOrders.Count);
}

/// <summary>
/// Thông tin rút gọn của 1 ảnh (Id, tên file) để liệt kê trong form sửa sản phẩm.
/// </summary>
public class ProductImageInfo
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu form thêm/sửa sản phẩm, kèm các ràng buộc nhập liệu (bắt buộc, độ dài, khoảng giá trị) và ảnh tải lên.
/// </summary>
public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    [StringLength(200, ErrorMessage = "Tên tối đa 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thương hiệu")]
    [StringLength(50)]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã model")]
    [StringLength(50)]
    public string Model { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    [Range(10, 150, ErrorMessage = "Kích thước từ 10 đến 150 inch")]
    public int ScreenSize { get; set; } = 55;

    [Required(ErrorMessage = "Vui lòng nhập độ phân giải")]
    [StringLength(30)]
    public string Resolution { get; set; } = "4K UHD";

    [Required(ErrorMessage = "Vui lòng nhập hệ điều hành")]
    [StringLength(30)]
    public string OperatingSystem { get; set; } = "Smart TV";

    [Range(0, 120, ErrorMessage = "Bảo hành từ 0 đến 120 tháng")]
    public int WarrantyMonths { get; set; } = 24;

    [Range(1, 1_000_000_000, ErrorMessage = "Giá bán không hợp lệ")]
    public decimal Price { get; set; }

    [Range(0, 1_000_000_000, ErrorMessage = "Giá gốc không hợp lệ")]
    public decimal? OldPrice { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>Ảnh mới tải lên (lưu vào bảng ProductImages).</summary>
    public List<IFormFile>? ImageFiles { get; set; }

    /// <summary>Id các ảnh cũ cần xóa.</summary>
    public List<int>? DeleteImageIds { get; set; }

    /// <summary>Ảnh hiện có (chỉ để hiển thị trong form sửa).</summary>
    public List<ProductImageInfo> ExistingImages { get; set; } = new();
}

/// <summary>
/// Dữ liệu form nhà cung cấp.
/// </summary>
public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên nhà cung cấp")]
    [StringLength(150, ErrorMessage = "Tên tối đa 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [RegularExpression(@"^[0-9+\-\.\s]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
    public string? Address { get; set; }
}

/// <summary>
/// Dữ liệu form nhân viên.
/// </summary>
public class EmployeeFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên nhân viên")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^[0-9+\-\.\s]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chức vụ")]
    [StringLength(50)]
    public string Position { get; set; } = "Nhân viên bán hàng";

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime HireDate { get; set; } = DateTime.Today;
}

/// <summary>
/// Dữ liệu form tạo mã giảm giá (phần trăm bắt buộc từ 20 đến 50).
/// </summary>
public class DiscountFormViewModel
{
    [RegularExpression(@"^[A-Za-z0-9_\-]{3,30}$", ErrorMessage = "Mã gồm 3-30 ký tự chữ, số, gạch ngang hoặc gạch dưới")]
    public string? Code { get; set; }

    [Range(20, 50, ErrorMessage = "Phần trăm giảm giá phải từ 20% đến 50%")]
    public int DiscountPercent { get; set; } = 20;

    public int? ProductId { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);

    [Range(1, 1_000_000, ErrorMessage = "Số lượt dùng tối đa phải lớn hơn 0")]
    public int? UsageLimit { get; set; }
}

/// <summary>
/// Dữ liệu form phiếu nhập hàng (nhà cung cấp, sản phẩm, số lượng, đơn giá).
/// </summary>
public class ImportFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhà cung cấp")]
    public int SupplierId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm")]
    public int ProductId { get; set; }

    [Range(1, 100000, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int Quantity { get; set; } = 1;

    [Range(0, 1_000_000_000, ErrorMessage = "Đơn giá không hợp lệ")]
    public decimal UnitPrice { get; set; }
}

// ---------- Đăng nhập ----------
/// <summary>
/// Dữ liệu form đăng nhập.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Dữ liệu form đổi mật khẩu (mật khẩu mới tối thiểu 8 ký tự, phải nhập lại khớp).
/// </summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8 đến 100 ký tự")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
