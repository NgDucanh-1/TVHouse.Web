using TVHouse.Web.Models;

namespace TVHouse.Web.Services;

/// <summary>
/// Kết quả kiểm tra mã giảm giá: có hợp lệ không, thông báo hiển thị cho khách, và số tiền được giảm.
/// </summary>
public class DiscountResult
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Kiểm tra mã giảm giá (20% - 50%) và tính số tiền được giảm.</summary>
public static class DiscountCalculator
{
    /// <summary>
    /// Kiểm tra mã giảm giá theo thứ tự: tồn tại -> còn bật -> đã đến ngày -> chưa hết hạn -> còn lượt dùng -> có sản phẩm phù hợp trong giỏ.
    /// Mã gắn 1 sản phẩm chỉ giảm tiền của sản phẩm đó; mã "tất cả sản phẩm" giảm trên cả giỏ. Số tiền giảm = tiền áp dụng x % / 100.
    /// </summary>
    public static DiscountResult Evaluate(
        DiscountCode? code,
        IReadOnlyCollection<(int ProductId, decimal LineTotal)> lines,
        DateTime today)
    {
        if (code == null)
            return Fail("Mã giảm giá không tồn tại");
        if (!code.IsActive)
            return Fail("Mã giảm giá đã bị tắt");
        if (today.Date < code.StartDate.Date)
            return Fail($"Mã chỉ có hiệu lực từ {code.StartDate:dd/MM/yyyy}");
        if (today.Date > code.EndDate.Date)
            return Fail("Mã giảm giá đã hết hạn");
        if (code.UsageLimit.HasValue && code.UsedCount >= code.UsageLimit.Value)
            return Fail("Mã giảm giá đã hết lượt sử dụng");

        // Mã theo sản phẩm chỉ giảm trên dòng của sản phẩm đó; mã chung giảm trên toàn bộ giỏ
        var eligible = code.ProductId == null
            ? lines.Sum(l => l.LineTotal)
            : lines.Where(l => l.ProductId == code.ProductId).Sum(l => l.LineTotal);

        if (eligible <= 0)
            return Fail("Mã này chỉ áp dụng cho một sản phẩm khác, không có trong giỏ hàng");

        var amount = Math.Round(eligible * code.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        return new DiscountResult
        {
            Valid = true,
            Amount = amount,
            Message = $"Đã áp dụng mã {code.Code}: giảm {code.DiscountPercent}%"
        };
    }

    /// <summary>
    /// Tạo kết quả "không hợp lệ" kèm câu thông báo lý do.
    /// </summary>
    private static DiscountResult Fail(string message) => new() { Valid = false, Message = message };
}
