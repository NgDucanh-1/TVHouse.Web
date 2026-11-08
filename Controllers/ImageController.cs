using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;

namespace TVHouse.Web.Controllers;

/// <summary>Trả ảnh sản phẩm đọc từ bảng ProductImages trong SQL Server.</summary>
public class ImageController : Controller
{
    private readonly AppDbContext _db;

    public ImageController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lấy ảnh theo Id của bảng ProductImages. Không có thì trả lỗi 404.
    /// Có thêm các header bảo mật (chặn chạy script trong ảnh SVG) và cho trình duyệt lưu đệm 1 giờ.
    /// </summary>
    [HttpGet("/img/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var img = await _db.ProductImages.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new { i.ContentType, i.ImageData })
            .FirstOrDefaultAsync();
        if (img == null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        // SVG/ảnh chỉ để hiển thị trong <img>, chặn mọi script nếu ai đó mở trực tiếp
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        Response.Headers["Cache-Control"] = "public,max-age=3600";
        return File(img.ImageData, img.ContentType);
    }
}
