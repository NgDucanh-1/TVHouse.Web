using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Data;
using TVHouse.Web.Services;

// ===== Program.cs: điểm khởi động của website - đăng ký các dịch vụ rồi khai báo thứ tự xử lý mỗi request =====

var builder = WebApplication.CreateBuilder(args);

// Kết nối SQL Server (chuỗi kết nối nằm trong appsettings.json)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC + tự động kiểm tra Anti-forgery token cho mọi request POST
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Giỏ hàng lưu trong Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// Đăng nhập bằng cookie: chưa đăng nhập mà vào /Admin sẽ bị chuyển tới /Account/Login
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "TVHouse.Auth";
        options.Cookie.HttpOnly = true;               // JavaScript không đọc được cookie
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // tự hết hạn sau 8 giờ không hoạt động
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CartService>();      // dịch vụ giỏ hàng (lưu trong Session)
builder.Services.AddScoped<SqlExportService>(); // dịch vụ xuất dữ liệu ra file .sql

var app = builder.Build();

// ----- Từ đây trở xuống: thứ tự xử lý mỗi request gửi tới website (thứ tự rất quan trọng) -----

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection(); // tự chuyển http -> https
app.UseStaticFiles();       // cho phép tải file trong thư mục wwwroot (css, js)
app.UseRouting();           // xác định request đi tới Controller/Action nào
app.UseSession();           // bật Session (giỏ hàng)
app.UseAuthentication(); // xác định "ai đang đăng nhập" (phải đứng trước UseAuthorization)
app.UseAuthorization();  // kiểm tra quyền truy cập ([Authorize])

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); // chạy website
