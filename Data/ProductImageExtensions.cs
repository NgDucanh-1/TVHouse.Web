using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Models;

namespace TVHouse.Web.Data;

public static class ProductImageExtensions
{
    /// <summary>
    /// Gán MainImageId (ảnh chính = SortOrder nhỏ nhất) cho danh sách sản phẩm.
    /// Chỉ đọc Id/SortOrder - KHÔNG tải dữ liệu ảnh nên rất nhẹ.
    /// </summary>
    public static async Task AttachMainImagesAsync(this AppDbContext db, IEnumerable<Product> products)
    {
        var list = products.ToList();
        var ids = list.Select(p => p.Id).Distinct().ToList();
        if (ids.Count == 0) return;

        var rows = await db.ProductImages.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .Select(i => new { i.Id, i.ProductId, i.SortOrder })
            .ToListAsync();

        var main = rows.GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).First().Id);

        foreach (var p in list)
            p.MainImageId = main.TryGetValue(p.Id, out var imageId) ? (int?)imageId : null;
    }
}
