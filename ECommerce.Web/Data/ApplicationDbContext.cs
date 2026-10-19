using ECommerce.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Coupon> Coupons => Set<Coupon>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cart>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        builder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        builder.Entity<CartItem>().Property(c => c.UnitPrice).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.CouponCode).HasMaxLength(50);
        builder.Entity<OrderItem>().Property(o => o.UnitPrice).HasPrecision(18, 2);

        var paymentTransaction = builder.Entity<PaymentTransaction>();
        paymentTransaction.Property(t => t.Amount).HasPrecision(18, 2);
        // SQL Server giới hạn khoá index 900 byte => phải có độ dài cột, không được nvarchar(max).
        paymentTransaction.Property(t => t.Provider).HasMaxLength(50);
        paymentTransaction.Property(t => t.TransactionRef).HasMaxLength(100);
        paymentTransaction.HasIndex(t => new { t.Provider, t.TransactionRef }).IsUnique();

        var address = builder.Entity<Address>();
        address.Property(a => a.RecipientName).HasMaxLength(100);
        address.Property(a => a.RecipientPhone).HasMaxLength(15);
        address.Property(a => a.AddressLine).HasMaxLength(300);
        address.HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        address.HasIndex(a => a.UserId);

        var coupon = builder.Entity<Coupon>();
        coupon.Property(c => c.Code).HasMaxLength(50);
        coupon.Property(c => c.Value).HasPrecision(18, 2);
        coupon.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
        coupon.HasIndex(c => c.Code).IsUnique();
    }
}