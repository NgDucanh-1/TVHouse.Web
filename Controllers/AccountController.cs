using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;
using TVHouse.Web.Models;
using TVHouse.Web.Services;

namespace TVHouse.Web.Controllers;

/// <summary>
/// Đăng nhập / đăng xuất / đổi mật khẩu cho trang Quản trị.
/// Tài khoản lưu trong bảng AdminUsers (SQL Server), đăng nhập xong server cấp 1 "cookie" xác thực.
/// </summary>
public class AccountController : Controller
{
    private const int MaxFailedAttempts = 5;                      // sai quá 5 lần thì khóa
    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(10); // khóa 10 phút

    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    /// <summary>Hiện trang đăng nhập. Nếu đã đăng nhập rồi thì chuyển thẳng vào trang Quản trị.</summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Admin");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>Xử lý form đăng nhập: kiểm tra tài khoản, khóa tạm khi sai nhiều lần, cấp cookie khi đúng.</summary>
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        const string wrongMessage = "Tên đăng nhập hoặc mật khẩu không đúng";
        var username = vm.Username.Trim();
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);

        // Không báo "sai tên" hay "sai mật khẩu" riêng để người lạ không dò được tên tài khoản
        if (user == null || !user.IsActive)
        {
            await Task.Delay(400); // làm chậm nhẹ để khó dò hàng loạt
            ModelState.AddModelError(string.Empty, wrongMessage);
            return View(vm);
        }

        // Đang bị khóa tạm?
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
        {
            ModelState.AddModelError(string.Empty,
                $"Tài khoản đang bị khóa tạm đến {user.LockoutEnd.Value:HH:mm} do nhập sai mật khẩu nhiều lần");
            return View(vm);
        }

        // Sai mật khẩu: tăng bộ đếm, đủ 5 lần thì khóa
        if (!PasswordHelper.Verify(vm.Password, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.Now.Add(LockoutTime);
                user.FailedAttempts = 0;
            }
            await _db.SaveChangesAsync();
            await Task.Delay(400);
            ModelState.AddModelError(string.Empty, wrongMessage);
            return View(vm);
        }

        // Đúng mật khẩu: xóa bộ đếm sai và cấp cookie đăng nhập
        user.FailedAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = vm.RememberMe }); // tick "Ghi nhớ" thì giữ đăng nhập lâu hơn

        // Quay về trang đang định vào (chỉ chấp nhận đường dẫn trong website để tránh bị lừa chuyển sang web lạ)
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Admin");
    }

    /// <summary>Đăng xuất (POST để tránh bị người khác ép đăng xuất bằng đường link).</summary>
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Toast"] = "Đã đăng xuất";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Hiện form đổi mật khẩu (phải đăng nhập).</summary>
    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    /// <summary>Đổi mật khẩu: kiểm tra mật khẩu cũ, rồi lưu chuỗi băm mới vào SQL Server.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idText, out var userId)) return Challenge();

        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null) return Challenge();

        if (!PasswordHelper.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Mật khẩu hiện tại không đúng");
            return View(vm);
        }

        user.PasswordHash = PasswordHelper.Hash(vm.NewPassword);
        await _db.SaveChangesAsync();

        TempData["Toast"] = "Đã đổi mật khẩu";
        return RedirectToAction("Index", "Admin");
    }
}
