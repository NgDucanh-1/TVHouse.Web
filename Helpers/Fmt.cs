using System.Globalization;

namespace TVHouse.Web.Helpers;

/// <summary>Định dạng tiền / ngày giống bản HTML gốc (vi-VN), không phụ thuộc ICU của máy chủ.</summary>
public static class Fmt
{
    /// <summary>
    /// Định dạng tiền: 10990000 -> "10.990.000 ₫".
    /// </summary>
    public static string Money(decimal n) =>
        n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.') + " ₫";

    /// <summary>
    /// Định dạng ngày: dd/MM/yyyy.
    /// </summary>
    public static string Date(DateTime d) =>
        d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Ví dụ 10990000 -> "10,99 tr"</summary>
    public static string Million(decimal n) =>
        (n / 1_000_000m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') + " tr";

    /// <summary>Đường dẫn ảnh sản phẩm lấy từ SQL Server (qua ImageController), null nếu chưa có ảnh.</summary>
    public static string? Image(int? imageId) =>
        imageId.HasValue ? "/img/" + imageId.Value.ToString(CultureInfo.InvariantCulture) : null;

    /// <summary>
    /// Mã sản phẩm hiển thị: Id 5 -> "#SP005".
    /// </summary>
    public static string ProductCode(int id) => "#SP" + id.ToString("000", CultureInfo.InvariantCulture);
    /// <summary>
    /// Mã khách hàng hiển thị: Id 5 -> "#KH005".
    /// </summary>
    public static string CustomerCode(int id) => "#KH" + id.ToString("000", CultureInfo.InvariantCulture);
}
