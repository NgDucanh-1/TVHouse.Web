using System.Security.Cryptography;

namespace TVHouse.Web.Services;

/// <summary>
/// Băm và kiểm tra mật khẩu bằng thuật toán PBKDF2-SHA256 (chuẩn bảo mật, không thể dịch ngược).
/// Chuỗi lưu trong cột AdminUsers.PasswordHash có dạng:
///     PBKDF2$số-vòng-lặp$muối(base64)$băm(base64)
/// Mỗi mật khẩu có "muối" ngẫu nhiên riêng nên 2 người trùng mật khẩu vẫn cho 2 chuỗi băm khác nhau.
/// </summary>
public static class PasswordHelper
{
    private const int Iterations = 100_000; // số vòng lặp: càng cao càng khó bị dò
    private const int SaltSize = 16;        // 16 byte muối
    private const int HashSize = 32;        // 32 byte kết quả băm

    /// <summary>Tạo chuỗi băm từ mật khẩu gốc (dùng khi đổi mật khẩu).</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Kiểm tra mật khẩu người dùng nhập có khớp với chuỗi băm đã lưu không.</summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations))
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        // So sánh "thời gian cố định" để kẻ tấn công không đoán được qua thời gian phản hồi
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
