using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;
using TVHouse.Web.Models;

namespace TVHouse.Web.Controllers;

/// <summary>
/// Trang chủ cửa hàng: hiển thị danh sách tivi lấy từ SQL Server (kèm ảnh), có lọc, tìm kiếm, sắp xếp.
/// Trang này ai cũng xem được, không cần đăng nhập.
/// </summary>
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    // Trang chủ: lọc theo danh mục, tìm kiếm, sắp xếp (thay cho renderProducts() của JS)
    /// <summary>
    /// Hiển thị trang chủ.
    /// - category: lọc theo danh mục (Tivi OLED, QLED, LED...)
    /// - q: từ khóa tìm theo tên / thương hiệu / model
    /// - sort: sắp xếp (featured = nổi bật, low = giá tăng dần, high = giá giảm dần)
    /// Chỉ hiện sản phẩm còn bán (IsActive = 1).
    /// </summary>
    public async Task<IActionResult> Index(string? category, string? q, string? sort)
    {
        category = string.IsNullOrWhiteSpace(category) || category == "Tất cả" ? "" : category.Trim();
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        sort = (sort is "low" or "high") ? sort : "featured";

        var query = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (category != "")
            query = query.Where(p => p.Category.Name == category);

        if (q != null)
            query = query.Where(p => p.Name.Contains(q) || p.Brand.Contains(q) || p.Model.Contains(q));

        IOrderedQueryable<Product> ordered = sort switch
        {
            "low" => query.OrderBy(p => p.Price),
            "high" => query.OrderByDescending(p => p.Price),
            _ => query.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Id)
        };

        var products = await ordered.ToListAsync();
        await _db.AttachMainImagesAsync(products);

        var vm = new ShopViewModel
        {
            Products = products,
            Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Id).ToListAsync(),
            ActiveCategory = category,
            Query = q,
            Sort = sort
        };
        return View(vm);
    }

    /// <summary>
    /// Trang báo lỗi chung khi hệ thống gặp sự cố ngoài dự kiến.
    /// </summary>
    public IActionResult Error() => View();
}
