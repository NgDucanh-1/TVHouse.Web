using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;
using TVHouse.Web.Models;
using TVHouse.Web.Services;

namespace TVHouse.Web.Controllers;

/// <summary>
/// Toàn bộ trang Quản trị: sản phẩm, kho, nhập hàng, nhà cung cấp, đơn hàng, hóa đơn, khách hàng, nhân viên, mã giảm giá, báo cáo.
/// Có thuộc tính [Authorize] nên PHẢI ĐĂNG NHẬP mới vào được.
/// </summary>
[Authorize] // toàn bộ trang Quản trị yêu cầu đăng nhập
public class AdminController : Controller
{
    private const int LowStockThreshold = 5;
    private const long MaxImageBytes = 3 * 1024 * 1024; // 3 MB / ảnh

    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".webp", "image/webp" }, { ".gif", "image/gif" }
    };

    private readonly AppDbContext _db;
    private readonly SqlExportService _export;

    public AdminController(AppDbContext db, SqlExportService export)
    {
        _db = db;
        _export = export;
    }

    // Thống kê hiển thị đầu mọi trang quản trị
    /// <summary>
    /// Chạy trước mọi trang quản trị: tính 4 số thống kê hiển thị ở đầu trang (số sản phẩm, đơn hàng, doanh thu, hàng sắp hết).
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ViewData["Stats"] = new AdminStats
        {
            ProductCount = await _db.Products.CountAsync(p => p.IsActive),
            OrderCount = await _db.Orders.CountAsync(),
            Revenue = await _db.Orders.Where(o => o.Status == OrderStatus.Completed)
                          .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m,
            LowStockCount = await _db.Products.CountAsync(p => p.IsActive && p.Stock <= LowStockThreshold)
        };
        await next();
    }

    // ===================== Tổng quan =====================
    /// <summary>
    /// Trang Tổng quan: các đơn hàng cần xử lý và cảnh báo hàng sắp hết (tồn kho từ 5 trở xuống).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var vm = new AdminOverviewViewModel
        {
            PendingOrders = await _db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Employee)
                .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.Id).ToListAsync(),
            LowStock = await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && p.Stock <= LowStockThreshold)
                .OrderBy(p => p.Stock).ThenBy(p => p.Id).ToListAsync()
        };
        return View(vm);
    }

    // Xuất toàn bộ dữ liệu hiện tại ra 1 file .sql (cấu trúc bảng + INSERT dữ liệu)
    /// <summary>
    /// Nút "Xuất file SQL": tải về 1 file .sql gồm cấu trúc bảng + toàn bộ dữ liệu hiện tại (kể cả ảnh) để sao lưu.
    /// </summary>
    public async Task<IActionResult> ExportSql()
    {
        try
        {
            var script = await _export.BuildScriptAsync();
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(script)).ToArray();
            return File(bytes, "application/sql", $"TVHouse_{DateTime.Now:yyyyMMdd_HHmmss}.sql");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
        {
            TempData["Toast"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // ===================== Sản phẩm =====================
    /// <summary>
    /// Danh sách sản phẩm (có ô tìm kiếm), kèm ảnh nhỏ lấy từ bảng ProductImages.
    /// </summary>
    public async Task<IActionResult> Products(string? q)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p => p.Name.Contains(q) || p.Brand.Contains(q) || p.Model.Contains(q));
        }
        var products = await query.OrderBy(p => p.Id).ToListAsync();
        await _db.AttachMainImagesAsync(products);
        ViewData["Q"] = q;
        return View(products);
    }

    /// <summary>
    /// Mở form thêm sản phẩm mới.
    /// </summary>
    public async Task<IActionResult> ProductCreate()
    {
        await LoadCategoriesAsync();
        return View("ProductForm", new ProductFormViewModel());
    }

    /// <summary>
    /// Mở form sửa sản phẩm, hiện sẵn thông tin cũ và các ảnh đang có.
    /// </summary>
    public async Task<IActionResult> ProductEdit(int id)
    {
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (p == null) return NotFound();

        var vm = new ProductFormViewModel
        {
            Id = p.Id, Name = p.Name, Brand = p.Brand, Model = p.Model, CategoryId = p.CategoryId,
            ScreenSize = p.ScreenSize, Resolution = p.Resolution, OperatingSystem = p.OperatingSystem,
            WarrantyMonths = p.WarrantyMonths, Price = p.Price, OldPrice = p.OldPrice,
            IsFeatured = p.IsFeatured
        };
        await LoadCategoriesAsync();
        await FillExistingImagesAsync(vm);
        return View("ProductForm", vm);
    }

    /// <summary>
    /// Lưu sản phẩm (thêm mới hoặc sửa).
    /// - Kiểm tra từng ảnh tải lên (đuôi file, dung lượng, nội dung có đúng là ảnh)
    /// - Lưu ảnh mới vào bảng ProductImages, xóa ảnh cũ được tick "Xóa"
    /// - Sản phẩm mới có tồn kho 0, tồn kho chỉ tăng khi nhập hàng.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProductSave(ProductFormViewModel vm)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == vm.CategoryId))
            ModelState.AddModelError(nameof(vm.CategoryId), "Danh mục không hợp lệ");

        // --- kiểm tra từng ảnh tải lên ---
        var files = (vm.ImageFiles ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
        foreach (var f in files)
        {
            var error = await ValidateImageAsync(f);
            if (error != null)
                ModelState.AddModelError(nameof(vm.ImageFiles), $"{Path.GetFileName(f.FileName)}: {error}");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            await FillExistingImagesAsync(vm);
            return View("ProductForm", vm);
        }

        Product? p;
        if (vm.Id == 0)
        {
            p = new Product { Stock = 0 }; // hàng mới = 0, tồn kho tăng qua phiếu nhập
            _db.Products.Add(p);
        }
        else
        {
            p = await _db.Products.FirstOrDefaultAsync(x => x.Id == vm.Id && x.IsActive);
            if (p == null) return NotFound();
        }

        p.Name = vm.Name.Trim();
        p.Brand = vm.Brand.Trim();
        p.Model = vm.Model.Trim();
        p.CategoryId = vm.CategoryId;
        p.ScreenSize = vm.ScreenSize;
        p.Resolution = vm.Resolution.Trim();
        p.OperatingSystem = vm.OperatingSystem.Trim();
        p.WarrantyMonths = vm.WarrantyMonths;
        p.Price = vm.Price;
        p.OldPrice = Math.Max(vm.OldPrice ?? 0m, vm.Price);
        p.IsFeatured = vm.IsFeatured;

        // --- ảnh mới -> bảng ProductImages (nối với sản phẩm qua ProductId) ---
        var nextSort = 0;
        if (vm.Id != 0)
            nextSort = ((await _db.ProductImages.Where(i => i.ProductId == vm.Id)
                            .MaxAsync(i => (int?)i.SortOrder)) ?? -1) + 1;

        foreach (var f in files)
        {
            using var ms = new MemoryStream();
            await f.CopyToAsync(ms);
            var fileName = Path.GetFileName(f.FileName);
            if (fileName.Length > 200) fileName = fileName[^200..];
            p.Images.Add(new ProductImage
            {
                FileName = fileName,
                ContentType = ImageTypes[Path.GetExtension(f.FileName)],
                ImageData = ms.ToArray(),
                SortOrder = nextSort++
            });
        }

        await _db.SaveChangesAsync();

        // --- xóa ảnh cũ được đánh dấu ---
        if (vm.Id != 0 && vm.DeleteImageIds is { Count: > 0 } deleteIds)
        {
            await _db.ProductImages
                .Where(i => i.ProductId == vm.Id && deleteIds.Contains(i.Id))
                .ExecuteDeleteAsync();
        }

        TempData["Toast"] = vm.Id == 0 ? "Đã thêm sản phẩm" : "Đã cập nhật sản phẩm";
        return RedirectToAction(nameof(Products));
    }

    // "Xóa" = ẩn sản phẩm (IsActive = 0) để không làm hỏng lịch sử đơn hàng/phiếu nhập
    /// <summary>
    /// "Xóa" sản phẩm = ẩn đi (IsActive = 0), không xóa thật để không làm hỏng lịch sử đơn hàng và phiếu nhập.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProductDelete(int id)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p != null)
        {
            p.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Đã xóa sản phẩm";
        }
        return RedirectToAction(nameof(Products));
    }

    // ===================== Kho hàng (chỉ xem + tìm theo tên) =====================
    /// <summary>
    /// Trang Kho hàng: CHỈ XEM tồn kho và tìm theo tên hàng, không sửa số lượng ở đây.
    /// </summary>
    public async Task<IActionResult> Inventory(string? q)
    {
        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p => p.Name.Contains(q));
        }
        var products = await query.OrderBy(p => p.Name).ToListAsync();
        await _db.AttachMainImagesAsync(products);
        ViewData["Q"] = q;
        return View(products);
    }

    // ===================== Nhập hàng =====================
    /// <summary>
    /// Danh sách phiếu nhập hàng (có nhà cung cấp).
    /// </summary>
    public async Task<IActionResult> Imports() =>
        View(await _db.ImportReceipts.AsNoTracking()
            .Include(r => r.Supplier).Include(r => r.Product)
            .OrderByDescending(r => r.Id).ToListAsync());

    /// <summary>
    /// Mở form tạo phiếu nhập hàng.
    /// </summary>
    public async Task<IActionResult> ImportCreate()
    {
        await LoadImportListsAsync();
        return View("ImportForm", new ImportFormViewModel());
    }

    // Tạo phiếu nhập -> cộng tồn kho
    /// <summary>
    /// Lưu phiếu nhập: bắt buộc chọn nhà cung cấp và sản phẩm, đồng thời CỘNG số lượng nhập vào tồn kho.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportSave(ImportFormViewModel vm)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == vm.SupplierId && s.IsActive);
        if (supplier == null)
            ModelState.AddModelError(nameof(vm.SupplierId), "Không tìm thấy nhà cung cấp");

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == vm.ProductId && p.IsActive);
        if (product == null)
            ModelState.AddModelError(nameof(vm.ProductId), "Không tìm thấy sản phẩm");

        if (!ModelState.IsValid || supplier == null || product == null)
        {
            await LoadImportListsAsync();
            return View("ImportForm", vm);
        }

        _db.ImportReceipts.Add(new ImportReceipt
        {
            ImportDate = DateTime.Now,
            SupplierId = supplier.Id,
            ProductId = product.Id,
            Quantity = vm.Quantity,
            UnitPrice = vm.UnitPrice
        });
        product.Stock += vm.Quantity;

        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã tạo phiếu nhập và cập nhật kho";
        return RedirectToAction(nameof(Imports));
    }

    // ===================== Nhà cung cấp =====================
    /// <summary>
    /// Danh sách nhà cung cấp kèm số phiếu nhập của từng nhà cung cấp.
    /// </summary>
    public async Task<IActionResult> Suppliers() =>
        View(await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Id)
            .Select(s => new SupplierRow
            {
                Supplier = s,
                ReceiptCount = _db.ImportReceipts.Count(r => r.SupplierId == s.Id)
            }).ToListAsync());

    /// <summary>
    /// Mở form thêm nhà cung cấp.
    /// </summary>
    public IActionResult SupplierCreate() => View("SupplierForm", new SupplierFormViewModel());

    /// <summary>
    /// Mở form sửa nhà cung cấp.
    /// </summary>
    public async Task<IActionResult> SupplierEdit(int id)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (s == null) return NotFound();
        return View("SupplierForm", new SupplierFormViewModel
        {
            Id = s.Id, Name = s.Name, ContactPerson = s.ContactPerson,
            Phone = s.Phone, Email = s.Email, Address = s.Address
        });
    }

    /// <summary>
    /// Lưu nhà cung cấp (thêm mới hoặc sửa).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SupplierSave(SupplierFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("SupplierForm", vm);

        Supplier? s;
        if (vm.Id == 0)
        {
            s = new Supplier();
            _db.Suppliers.Add(s);
        }
        else
        {
            s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == vm.Id && x.IsActive);
            if (s == null) return NotFound();
        }

        s.Name = vm.Name.Trim();
        s.ContactPerson = Clean(vm.ContactPerson);
        s.Phone = Clean(vm.Phone);
        s.Email = Clean(vm.Email);
        s.Address = Clean(vm.Address);

        await _db.SaveChangesAsync();
        TempData["Toast"] = vm.Id == 0 ? "Đã thêm nhà cung cấp" : "Đã cập nhật nhà cung cấp";
        return RedirectToAction(nameof(Suppliers));
    }

    // Ẩn nhà cung cấp (giữ nguyên lịch sử phiếu nhập)
    /// <summary>
    /// Ẩn nhà cung cấp (không xóa thật để giữ lịch sử phiếu nhập).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SupplierDelete(int id)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (s != null)
        {
            s.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Đã xóa nhà cung cấp";
        }
        return RedirectToAction(nameof(Suppliers));
    }

    // ===================== Đơn hàng =====================
    /// <summary>
    /// Danh sách đơn hàng kèm khách hàng, nhân viên tư vấn, mã giảm giá.
    /// </summary>
    public async Task<IActionResult> Orders() =>
        View(await _db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Employee)
            .Include(o => o.DiscountCode)
            .OrderByDescending(o => o.Id).ToListAsync());

    /// <summary>
    /// Đổi trạng thái đơn hàng.
    /// - Hủy đơn: hoàn lại tồn kho và lượt dùng mã giảm giá
    /// - Mở lại đơn đã hủy: trừ kho lại (nếu đủ hàng)
    /// - Chuyển sang "Hoàn thành": tự tạo hóa đơn nếu chưa có.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateOrderStatus(int id, string status)
    {
        if (!OrderStatus.Labels.ContainsKey(status))
        {
            TempData["Toast"] = "Trạng thái không hợp lệ";
            return RedirectToAction(nameof(Orders));
        }

        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Invoice)
            .Include(o => o.DiscountCode)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();

        var old = order.Status;
        if (old != status)
        {
            if (status == OrderStatus.Cancelled)
            {
                // Hủy đơn -> hoàn lại tồn kho và lượt dùng mã giảm giá
                foreach (var item in order.Items) item.Product.Stock += item.Quantity;
                if (order.DiscountCode != null && order.DiscountCode.UsedCount > 0)
                    order.DiscountCode.UsedCount--;
            }
            else if (old == OrderStatus.Cancelled)
            {
                // Mở lại đơn đã hủy -> trừ kho lần nữa (nếu đủ hàng)
                if (order.Items.Any(i => i.Product.Stock < i.Quantity))
                {
                    TempData["Toast"] = "Không đủ tồn kho để mở lại đơn hàng này";
                    return RedirectToAction(nameof(Orders));
                }
                foreach (var item in order.Items) item.Product.Stock -= item.Quantity;
                if (order.DiscountCode != null) order.DiscountCode.UsedCount++;
            }

            order.Status = status;

            // Đơn hoàn thành -> bảo đảm có hóa đơn bán hàng
            if (status == OrderStatus.Completed && order.Invoice == null)
            {
                _db.Invoices.Add(new Invoice
                {
                    OrderId = order.Id,
                    InvoiceDate = DateTime.Now,
                    TotalAmount = order.TotalAmount
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã cập nhật trạng thái đơn hàng";
        return RedirectToAction(nameof(Orders));
    }

    // ===================== Hóa đơn =====================
    /// <summary>
    /// Danh sách hóa đơn bán hàng (khách, nhân viên tư vấn, thanh toán, mã giảm giá).
    /// </summary>
    public async Task<IActionResult> Invoices() =>
        View(await _db.Invoices.AsNoTracking()
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.Employee)
            .Include(i => i.Order).ThenInclude(o => o.DiscountCode)
            .OrderByDescending(i => i.Id).ToListAsync());

    /// <summary>
    /// Mở hóa đơn đầy đủ (tên khách, nhân viên tư vấn, giảm giá...) và tự bật hộp thoại in.
    /// </summary>
    public async Task<IActionResult> PrintInvoice(int id)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.Employee)
            .Include(i => i.Order).ThenInclude(o => o.DiscountCode)
            .Include(i => i.Order).ThenInclude(o => o.Items).ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();
        return View("~/Views/Shared/PrintInvoice.cshtml", invoice);
    }

    // ===================== Khách hàng =====================
    /// <summary>
    /// Danh sách khách hàng và số đơn đã mua.
    /// </summary>
    public async Task<IActionResult> Customers() =>
        View(await _db.Customers.AsNoTracking().Include(c => c.Orders).OrderBy(c => c.Id).ToListAsync());

    // ===================== Nhân viên bán hàng =====================
    /// <summary>
    /// Danh sách nhân viên bán hàng kèm số hóa đơn và doanh thu mà mỗi người đã tư vấn (chỉ tính đơn hoàn thành).
    /// </summary>
    public async Task<IActionResult> Employees()
    {
        var stats = await _db.Orders.AsNoTracking()
            .Where(o => o.EmployeeId != null && o.Status == OrderStatus.Completed)
            .GroupBy(o => o.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync();
        var byEmployee = stats.Where(x => x.EmployeeId.HasValue).ToDictionary(x => x.EmployeeId!.Value);

        var employees = await _db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.Id).ToListAsync();
        var rows = employees.Select(e => new EmployeeRow
        {
            Employee = e,
            InvoiceCount = byEmployee.TryGetValue(e.Id, out var s) ? s.Count : 0,
            Revenue = byEmployee.TryGetValue(e.Id, out var s2) ? s2.Revenue : 0m
        }).ToList();
        return View(rows);
    }

    /// <summary>
    /// Mở form thêm nhân viên.
    /// </summary>
    public IActionResult EmployeeCreate() => View("EmployeeForm", new EmployeeFormViewModel());

    /// <summary>
    /// Mở form sửa nhân viên.
    /// </summary>
    public async Task<IActionResult> EmployeeEdit(int id)
    {
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (e == null) return NotFound();
        return View("EmployeeForm", new EmployeeFormViewModel
        {
            Id = e.Id, FullName = e.FullName, Phone = e.Phone, Email = e.Email,
            Position = e.Position, HireDate = e.HireDate
        });
    }

    /// <summary>
    /// Lưu nhân viên (thêm mới hoặc sửa).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EmployeeSave(EmployeeFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("EmployeeForm", vm);

        Employee? e;
        if (vm.Id == 0)
        {
            e = new Employee();
            _db.Employees.Add(e);
        }
        else
        {
            e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == vm.Id && x.IsActive);
            if (e == null) return NotFound();
        }

        e.FullName = vm.FullName.Trim();
        e.Phone = Clean(vm.Phone);
        e.Email = Clean(vm.Email);
        e.Position = vm.Position.Trim();
        e.HireDate = vm.HireDate.Date;

        await _db.SaveChangesAsync();
        TempData["Toast"] = vm.Id == 0 ? "Đã thêm nhân viên" : "Đã cập nhật nhân viên";
        return RedirectToAction(nameof(Employees));
    }

    // Cho nghỉ việc (ẩn) - hóa đơn cũ vẫn hiển thị tên nhân viên đã tư vấn
    /// <summary>
    /// Cho nhân viên nghỉ việc (ẩn đi), các hóa đơn cũ vẫn giữ tên nhân viên đã tư vấn.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EmployeeDelete(int id)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id);
        if (e != null)
        {
            e.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Đã cho nhân viên nghỉ việc";
        }
        return RedirectToAction(nameof(Employees));
    }

    // ===================== Mã giảm giá (20% - 50%) =====================
    /// <summary>
    /// Danh sách mã giảm giá và trạng thái (đang áp dụng, hết hạn, hết lượt, đã tắt...).
    /// </summary>
    public async Task<IActionResult> Discounts() =>
        View(await _db.DiscountCodes.AsNoTracking().Include(d => d.Product)
            .OrderByDescending(d => d.Id).ToListAsync());

    /// <summary>
    /// Mở form tạo mã giảm giá.
    /// </summary>
    public async Task<IActionResult> DiscountCreate()
    {
        await LoadDiscountProductsAsync();
        return View("DiscountForm", new DiscountFormViewModel());
    }

    /// <summary>
    /// Lưu mã giảm giá mới: phần trăm từ 20 đến 50, áp dụng cho 1 sản phẩm hoặc tất cả.
    /// Để trống ô mã thì tự sinh mã ngẫu nhiên (ví dụ GIAM30-A1B2C). Mã không được trùng.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DiscountSave(DiscountFormViewModel vm)
    {
        var code = (vm.Code ?? string.Empty).Trim().ToUpperInvariant();

        if (vm.EndDate.Date < vm.StartDate.Date)
            ModelState.AddModelError(nameof(vm.EndDate), "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu");

        if (vm.ProductId.HasValue && !await _db.Products.AnyAsync(p => p.Id == vm.ProductId && p.IsActive))
            ModelState.AddModelError(nameof(vm.ProductId), "Sản phẩm không hợp lệ");

        if (code.Length > 0 && await _db.DiscountCodes.AnyAsync(d => d.Code == code))
            ModelState.AddModelError(nameof(vm.Code), "Mã này đã tồn tại");

        if (!ModelState.IsValid)
        {
            await LoadDiscountProductsAsync();
            return View("DiscountForm", vm);
        }

        if (code.Length == 0)
        {
            // Để trống -> tự sinh mã, ví dụ GIAM30-A1B2C
            do
            {
                code = $"GIAM{vm.DiscountPercent}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";
            }
            while (await _db.DiscountCodes.AnyAsync(d => d.Code == code));
        }

        _db.DiscountCodes.Add(new DiscountCode
        {
            Code = code,
            DiscountPercent = vm.DiscountPercent,
            ProductId = vm.ProductId,
            StartDate = vm.StartDate.Date,
            EndDate = vm.EndDate.Date,
            UsageLimit = vm.UsageLimit,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"Đã tạo mã giảm giá {code}";
        return RedirectToAction(nameof(Discounts));
    }

    /// <summary>
    /// Bật / tắt một mã giảm giá.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DiscountToggle(int id)
    {
        var d = await _db.DiscountCodes.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        d.IsActive = !d.IsActive;
        await _db.SaveChangesAsync();
        TempData["Toast"] = d.IsActive ? "Đã bật mã giảm giá" : "Đã tắt mã giảm giá";
        return RedirectToAction(nameof(Discounts));
    }

    // Chỉ xóa được mã chưa từng gắn vào đơn hàng nào
    /// <summary>
    /// Xóa mã giảm giá. Mã đã dùng trong đơn hàng thì không xóa được (chỉ tắt).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DiscountDelete(int id)
    {
        var d = await _db.DiscountCodes.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();

        if (await _db.Orders.AnyAsync(o => o.DiscountCodeId == id))
        {
            TempData["Toast"] = "Mã đã được dùng trong đơn hàng, chỉ có thể tắt chứ không xóa";
            return RedirectToAction(nameof(Discounts));
        }

        _db.DiscountCodes.Remove(d);
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Đã xóa mã giảm giá";
        return RedirectToAction(nameof(Discounts));
    }

    // ===================== Báo cáo =====================
    /// <summary>
    /// Báo cáo doanh thu: tổng doanh thu, số đơn, giá trị trung bình, tổng tiền đã giảm (chỉ tính đơn hoàn thành).
    /// </summary>
    public async Task<IActionResult> Reports()
    {
        var vm = new ReportViewModel
        {
            CompletedOrders = await _db.Orders.AsNoTracking()
                .Include(o => o.Customer).Include(o => o.Employee)
                .Where(o => o.Status == OrderStatus.Completed)
                .OrderByDescending(o => o.Id).ToListAsync()
        };
        return View(vm);
    }

    // ===================== Helpers =====================
    /// <summary>
    /// Cắt khoảng trắng thừa; chuỗi rỗng đổi thành null để lưu NULL vào SQL.
    /// </summary>
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Nạp danh sách danh mục cho ô chọn (dropdown) trong form sản phẩm.
    /// </summary>
    private async Task LoadCategoriesAsync()
    {
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }

    /// <summary>
    /// Nạp danh sách ảnh hiện có của sản phẩm (chỉ Id và tên file, không tải dữ liệu ảnh) để hiện trong form sửa.
    /// </summary>
    private async Task FillExistingImagesAsync(ProductFormViewModel vm)
    {
        if (vm.Id == 0) return;
        vm.ExistingImages = await _db.ProductImages.AsNoTracking()
            .Where(i => i.ProductId == vm.Id)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new ProductImageInfo { Id = i.Id, FileName = i.FileName })
            .ToListAsync();
    }

    /// <summary>
    /// Nạp danh sách nhà cung cấp và sản phẩm cho form phiếu nhập.
    /// </summary>
    private async Task LoadImportListsAsync()
    {
        ViewBag.Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.SupplierCode + " — " + s.Name })
            .ToListAsync();
        ViewBag.Products = await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name + " (" + p.Model + ")" })
            .ToListAsync();
    }

    /// <summary>
    /// Nạp danh sách sản phẩm cho form mã giảm giá (có thêm lựa chọn "Tất cả sản phẩm").
    /// </summary>
    private async Task LoadDiscountProductsAsync()
    {
        var items = new List<SelectListItem> { new("Tất cả sản phẩm", "") };
        items.AddRange(await _db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name + " (" + p.Model + ")" })
            .ToListAsync());
        ViewBag.DiscountProducts = items;
    }

    /// <summary>Chỉ nhận ảnh jpg/png/webp/gif ≤ 3 MB và kiểm tra cả "chữ ký" đầu file.</summary>
    private static async Task<string?> ValidateImageAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!ImageTypes.ContainsKey(ext))
            return "chỉ chấp nhận .jpg, .jpeg, .png, .webp, .gif";
        if (file.Length > MaxImageBytes)
            return "ảnh tối đa 3 MB";

        var h = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
            read = await stream.ReadAsync(h, 0, h.Length);

        var ok = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => read >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => read >= 4 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
            ".gif" => read >= 4 && h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38,
            ".webp" => read >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                                  && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
            _ => false
        };
        return ok ? null : "nội dung file không phải ảnh hợp lệ";
    }
}
