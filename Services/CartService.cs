using System.Text.Json;
using TVHouse.Web.Models;

namespace TVHouse.Web.Services;

/// <summary>Giỏ hàng lưu trong Session (JSON), thay cho mảng `cart` của bản HTML.</summary>
public class CartService
{
    private const string Key = "tvhouse_cart";
    private readonly IHttpContextAccessor _accessor;

    public CartService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession Session => _accessor.HttpContext!.Session;

    /// <summary>
    /// Lấy toàn bộ dòng trong giỏ hàng.
    /// </summary>
    public List<CartLine> Get()
    {
        var json = Session.GetString(Key);
        if (string.IsNullOrEmpty(json)) return new List<CartLine>();
        return JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
    }

    /// <summary>
    /// Ghi lại giỏ hàng vào Session.
    /// </summary>
    private void Save(List<CartLine> lines) =>
        Session.SetString(Key, JsonSerializer.Serialize(lines));

    /// <summary>
    /// Tổng số lượng sản phẩm trong giỏ (hiển thị số trên nút Giỏ hàng).
    /// </summary>
    public int Count() => Get().Sum(l => l.Quantity);

    /// <summary>
    /// Số lượng của 1 sản phẩm đang có trong giỏ.
    /// </summary>
    public int QuantityOf(int productId) =>
        Get().FirstOrDefault(l => l.ProductId == productId)?.Quantity ?? 0;

    /// <summary>
    /// Thêm 1 sản phẩm: chưa có thì tạo dòng mới, có rồi thì tăng số lượng thêm 1.
    /// </summary>
    public void Add(int productId)
    {
        var lines = Get();
        var line = lines.FirstOrDefault(l => l.ProductId == productId);
        if (line == null) lines.Add(new CartLine { ProductId = productId, Quantity = 1 });
        else line.Quantity++;
        Save(lines);
    }

    /// <summary>
    /// Xóa hẳn 1 sản phẩm khỏi giỏ.
    /// </summary>
    public void Remove(int productId)
    {
        var lines = Get();
        lines.RemoveAll(l => l.ProductId == productId);
        Save(lines);
    }

    /// <summary>
    /// Xóa sạch giỏ hàng (sau khi thanh toán xong).
    /// </summary>
    public void Clear() => Session.Remove(Key);
}
