using Microsoft.EntityFrameworkCore;
using TVHouse.Web.Models;

namespace TVHouse.Web.Data;

/// <summary>
/// DbContext ánh xạ tới database TVHouseDB (tạo bởi Database/TVHouse.sql).
/// Tên bảng/cột trùng với tên DbSet/thuộc tính nên không cần migration.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ImportReceipt> ImportReceipts => Set<ImportReceipt>();

    /// <summary>
    /// Tạo câu lệnh SQL cho các cột mã tự sinh (DH0001, HD0001, PN001, NV001, NCC001...) ghép từ tiền tố + Id.
    /// </summary>
    private static string Code(string prefix, int width, string col = "Id") =>
        $"'{prefix}' + REPLICATE('0', CASE WHEN {col} < {(int)Math.Pow(10, width - 1)} THEN {width} - LEN(CONVERT(VARCHAR(10), {col})) ELSE 0 END) + CONVERT(VARCHAR(10), {col})";

    /// <summary>
    /// Khai báo cách ánh xạ bảng SQL: độ chính xác tiền, cột tính toán, khóa ngoại và kiểu xóa liên quan (xóa dây chuyền, đặt NULL, cấm xóa).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AdminUser>(e => e.HasIndex(u => u.Username).IsUnique());

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 0);
            e.Property(p => p.OldPrice).HasPrecision(18, 0);
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
        });

        b.Entity<ProductImage>(e =>
        {
            e.Property(i => i.ImageData).HasColumnType("varbinary(max)");
            e.HasOne(i => i.Product).WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Supplier>(e =>
            e.Property(s => s.SupplierCode).HasComputedColumnSql(Code("NCC", 3), stored: true));

        b.Entity<Employee>(e =>
        {
            e.Property(x => x.EmployeeCode).HasComputedColumnSql(Code("NV", 3), stored: true);
            e.Property(x => x.HireDate).HasColumnType("date");
        });

        b.Entity<DiscountCode>(e =>
        {
            e.Property(d => d.StartDate).HasColumnType("date");
            e.Property(d => d.EndDate).HasColumnType("date");
            e.HasIndex(d => d.Code).IsUnique();
            e.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Order>(e =>
        {
            e.Property(o => o.SubtotalAmount).HasPrecision(18, 0);
            e.Property(o => o.DiscountAmount).HasPrecision(18, 0);
            e.Property(o => o.TotalAmount).HasPrecision(18, 0);
            // Cột tính toán do SQL Server sinh -> EF chỉ đọc, không ghi
            e.Property(o => o.OrderCode).HasComputedColumnSql(Code("DH", 4), stored: true);
            e.HasOne(o => o.Customer).WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.Employee).WithMany()
                .HasForeignKey(o => o.EmployeeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.DiscountCode).WithMany()
                .HasForeignKey(o => o.DiscountCodeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 0);
            e.HasOne(i => i.Order).WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany()
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Invoice>(e =>
        {
            e.Property(i => i.TotalAmount).HasPrecision(18, 0);
            e.Property(i => i.InvoiceCode).HasComputedColumnSql(Code("HD", 4), stored: true);
            e.HasOne(i => i.Order).WithOne(o => o.Invoice)
                .HasForeignKey<Invoice>(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ImportReceipt>(e =>
        {
            e.Property(r => r.UnitPrice).HasPrecision(18, 0);
            e.Property(r => r.ReceiptCode).HasComputedColumnSql(Code("PN", 3), stored: true);
            e.HasOne(r => r.Supplier).WithMany()
                .HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Product).WithMany()
                .HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
