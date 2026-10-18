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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cart>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        builder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        builder.Entity<CartItem>().Property(c => c.UnitPrice).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Entity<OrderItem>().Property(o => o.UnitPrice).HasPrecision(18, 2);

        var paymentTransaction = builder.Entity<PaymentTransaction>();
        paymentTransaction.Property(t => t.Amount).HasPrecision(18, 2);
        // SQL Server giới hạn khoá index 900 byte => phải có độ dài cột, không được nvarchar(max).
        paymentTransaction.Property(t => t.Provider).HasMaxLength(50);
        paymentTransaction.Property(t => t.TransactionRef).HasMaxLength(100);
        paymentTransaction.HasIndex(t => new { t.Provider, t.TransactionRef }).IsUnique();
    }
}