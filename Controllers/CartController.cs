using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;
using TVHouse.Web.Models;
using TVHouse.Web.Services;

namespace TVHouse.Web.Controllers;

/// <summary>
/// Giỏ hàng và xuất hóa đơn cho khách mua tại quầy.
/// Giỏ hàng lưu trong Session (xem CartService); khi thanh toán sẽ ghi Orders, OrderItems, Invoices và trừ tồn kho vào SQL Server.
/// </summary>
public class CartController : Controller
{
    private const string LastInvoiceKey = "LastInvoiceId"; // nhớ hóa đơn vừa xuất trong phiên làm việc

    private readonly AppDbContext _db;
    private readonly CartService _cart;

    public CartController(AppDbContext db, CartService cart)
    {
        _db = db;
        _cart = cart;
    }

    // ===================== Giỏ hàng + form xuất hóa đơn =====================
    /// <summary>
    /// Hiển thị trang giỏ hàng cùng form thông tin khách, nhân viên tư vấn, phương thức thanh toán, mã giảm giá.
    /// </summary>
    public async Task<IActionResult> Index() =>
        View(await BuildAsync(new CheckoutViewModel()));

    /// <summary>
    /// Thêm 1 sản phẩm vào giỏ (nút "Thêm giỏ").
    /// Không cho thêm nếu hết hàng hoặc số lượng trong giỏ đã bằng tồn kho. Xong quay lại trang trước đó (returnUrl).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Add(int id, string? returnUrl)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null || product.Stock < 1)
            TempData["Toast"] = "Sản phẩm hiện đã hết hàng";
        else if (_cart.QuantityOf(id) >= product.Stock)
            TempData["Toast"] = "Số lượng trong giỏ đã bằng tồn kho";
        else
        {
            _cart.Add(id);
            TempData["Toast"] = "Đã thêm sản phẩm vào giỏ hàng";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Xóa 1 sản phẩm khỏi giỏ hàng.
    /// </summary>
    [HttpPost]
    public IActionResult Remove(int id)
    {
        _cart.Remove(id);
        TempData["Toast"] = "Đã xóa sản phẩm khỏi giỏ hàng";
        return RedirectToAction(nameof(Index));
    }

    // Nút "Áp dụng" mã giảm giá: chỉ xem trước số tiền giảm, chưa tạo hóa đơn
    /// <summary>
    /// Nút "Áp dụng" mã giảm giá: chỉ TÍNH THỬ số tiền được giảm để khách xem, chưa tạo hóa đơn.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ApplyCode(CheckoutViewModel form)
    {
        ModelState.Clear(); // người dùng có thể chưa điền xong form
        return View("Index", await BuildAsync(form));
    }

    // ===================== Thanh toán & xuất hóa đơn =====================
    /// <summary>
    /// Nút "Thanh toán & xuất hóa đơn". Các bước:
    /// 1) Kiểm tra form (tên khách, nhân viên tư vấn, phương thức thanh toán)
    /// 2) Kiểm tra tồn kho và mã giảm giá
    /// 3) Tạo/cập nhật khách hàng (trùng số điện thoại thì dùng lại khách cũ)
    /// 4) Tạo đơn hàng + chi tiết, trừ kho, tăng lượt dùng mã, tạo hóa đơn
    /// Tất cả lưu trong cùng 1 giao dịch SQL (lỗi giữa chừng thì không lưu gì).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Checkout(CheckoutViewModel form)
    {
        var cartLines = _cart.Get();
        if (cartLines.Count == 0)
        {
            TempData["Toast"] = "Giỏ hàng đang trống";
            return RedirectToAction(nameof(Index));
        }

        // --- kiểm tra dữ liệu form ---
        form.CustomerName = (form.CustomerName ?? string.Empty).Trim();
        var phone = NormalizePhone(form.Phone);

        if (!PaymentMethods.All.Contains(form.PaymentMethod))
            ModelState.AddModelError(nameof(form.PaymentMethod), "Phương thức thanh toán không hợp lệ");

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == form.EmployeeId && e.IsActive);
        if (employee == null)
            ModelState.AddModelError(nameof(form.EmployeeId), "Vui lòng chọn nhân viên tư vấn");

        // --- kiểm tra tồn kho (nạp sản phẩm có theo dõi thay đổi để trừ kho) ---
        var ids = cartLines.Select(l => l.ProductId).ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id);

        var items = new List<OrderItem>();
        foreach (var line in cartLines)
        {
            products.TryGetValue(line.ProductId, out var product);
            if (product == null || product.Stock < line.Quantity)
            {
                TempData["Toast"] = "Có sản phẩm không còn đủ tồn kho, vui lòng kiểm tra lại giỏ hàng";
                return RedirectToAction(nameof(Index));
            }
            items.Add(new OrderItem { ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price });
        }

        var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);

        // --- mã giảm giá (nếu có) ---
        DiscountCode? code = null;
        decimal discount = 0;
        var codeText = form.DiscountCode?.Trim();
        if (!string.IsNullOrEmpty(codeText))
        {
            code = await _db.DiscountCodes.FirstOrDefaultAsync(d => d.Code == codeText);
            var result = DiscountCalculator.Evaluate(
                code,
                items.Select(i => (i.ProductId, i.UnitPrice * i.Quantity)).ToList(),
                DateTime.Today);
            if (result.Valid) discount = result.Amount;
            else ModelState.AddModelError(nameof(form.DiscountCode), result.Message);
        }

        if (!ModelState.IsValid)
            return View("Index", await BuildAsync(form));

        // --- khách hàng mua tại quầy: dùng lại khách cũ nếu trùng số điện thoại ---
        Customer? customer = null;
        if (phone != null)
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer == null)
        {
            customer = new Customer();
            _db.Customers.Add(customer);
        }
        customer.FullName = form.CustomerName;
        customer.Phone = phone;
        if (!string.IsNullOrWhiteSpace(form.Email)) customer.Email = form.Email.Trim();

        // --- tạo đơn hàng + trừ kho + hóa đơn trong cùng 1 giao dịch ---
        foreach (var item in items)
            products[item.ProductId].Stock -= item.Quantity;

        var order = new Order
        {
            Customer = customer,
            EmployeeId = employee!.Id,
            OrderDate = DateTime.Now,
            SubtotalAmount = subtotal,
            DiscountAmount = discount,
            TotalAmount = subtotal - discount,
            PaymentMethod = form.PaymentMethod,
            Status = OrderStatus.Completed, // bán tại quầy: thanh toán xong là hoàn thành
            Items = items
        };

        if (discount > 0 && code != null)
        {
            order.DiscountCode = code;
            code.UsedCount++;
        }

        var invoice = new Invoice
        {
            Order = order,
            InvoiceDate = DateTime.Now,
            TotalAmount = order.TotalAmount
        };

        _db.Orders.Add(order);
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        _cart.Clear();
        TempData["Toast"] = $"Đã xuất hóa đơn {invoice.InvoiceCode}";
        HttpContext.Session.SetInt32(LastInvoiceKey, invoice.Id);
        return RedirectToAction(nameof(Receipt), new { id = invoice.Id });
    }

    // Xem/in hóa đơn vừa xuất. Người bán chưa đăng nhập chỉ xem được hóa đơn VỪA xuất trong phiên này
    // (tránh lộ hóa đơn của khách khác khi đoán số id). Admin đã đăng nhập xem được mọi hóa đơn.
    /// <summary>
    /// Hiển thị hóa đơn vừa xuất để in. Người chưa đăng nhập chỉ xem được hóa đơn vừa xuất trong phiên này.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        if (User.Identity?.IsAuthenticated != true && HttpContext.Session.GetInt32(LastInvoiceKey) != id)
            return Challenge();

        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.Employee)
            .Include(i => i.Order).ThenInclude(o => o.DiscountCode)
            .Include(i => i.Order).ThenInclude(o => o.Items).ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();
        return View("~/Views/Shared/PrintInvoice.cshtml", invoice);
    }

    // ===================== Helpers =====================
    /// <summary>
    /// Chuẩn bị dữ liệu hiển thị trang giỏ hàng: danh sách hàng + ảnh, danh sách nhân viên, và tính thử giảm giá nếu khách đã nhập mã.
    /// </summary>
    private async Task<CheckoutViewModel> BuildAsync(CheckoutViewModel vm)
    {
        var cartLines = _cart.Get();
        var ids = cartLines.Select(l => l.ProductId).ToList();

        var products = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .ToListAsync();
        await _db.AttachMainImagesAsync(products);
        var byId = products.ToDictionary(p => p.Id);

        vm.Lines = new List<CartLineViewModel>();
        foreach (var line in cartLines)
        {
            if (byId.TryGetValue(line.ProductId, out var product))
                vm.Lines.Add(new CartLineViewModel { Product = product, Quantity = line.Quantity });
        }

        vm.Employees = await _db.Employees.AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName + " — " + e.Position })
            .ToListAsync();

        // Xem trước mã giảm giá
        vm.DiscountAmount = 0;
        vm.DiscountValid = false;
        vm.DiscountMessage = null;
        var codeText = vm.DiscountCode?.Trim();
        if (!string.IsNullOrEmpty(codeText))
        {
            var code = await _db.DiscountCodes.AsNoTracking().FirstOrDefaultAsync(d => d.Code == codeText);
            var result = DiscountCalculator.Evaluate(
                code,
                vm.Lines.Select(l => (l.Product.Id, l.LineTotal)).ToList(),
                DateTime.Today);
            vm.DiscountValid = result.Valid;
            vm.DiscountMessage = result.Message;
            vm.DiscountAmount = result.Valid ? result.Amount : 0;
        }

        return vm;
    }

    /// <summary>
    /// Làm sạch số điện thoại: chỉ giữ chữ số và dấu +. Để trống thì trả về null.
    /// </summary>
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var p = new string(phone.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
        return p.Length == 0 ? null : p;
    }
}
