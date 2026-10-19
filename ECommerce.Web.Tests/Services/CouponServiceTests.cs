using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Models.Entities.Enums;
using ECommerce.Web.Models.ViewModels;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Tests.Services;

public class CouponServiceTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<(ApplicationDbContext Db, SqliteConnection Connection, CouponService Service)> CreateAsync(
        int usageLimit = 5)
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.Coupons.Add(new Coupon
        {
            Code = "SAVE10",
            DiscountType = DiscountType.Percentage,
            Value = 10m,
            MinOrderAmount = 0m,
            UsageLimit = usageLimit,
            UsedCount = 0,
            StartsOn = Today.AddDays(-1),
            EndsOn = Today.AddDays(1),
            IsActive = true
        });
        await db.SaveChangesAsync();
        return (db, connection, new CouponService(db));
    }

    private static CouponFormViewModel Form(string code = "SAVE10", decimal value = 10m) => new()
    {
        Code = code,
        DiscountType = DiscountType.Percentage,
        Value = value,
        MinOrderAmount = 0m,
        UsageLimit = 5,
        StartsOn = Today,
        EndsOn = Today.AddDays(7),
        IsActive = true
    };

    [Fact]
    public async Task ValidateAsync_BlankCode_NoDiscountNoError()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.ValidateAsync("   ", 1_000_000m);

        Assert.False(result.IsValid);
        Assert.Null(result.Error);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_Percentage_ComputesRoundedDiscount()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.ValidateAsync("SAVE10", 1_234_567m);

        Assert.True(result.IsValid);
        Assert.Equal(123_457m, result.DiscountAmount);
        Assert.Equal("SAVE10", result.Code);
        Assert.Contains("10%", result.Description);
    }

    [Fact]
    public async Task ValidateAsync_Fixed_CapsAtSubtotal()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.DiscountType = DiscountType.FixedAmount;
        coupon.Value = 500_000m;
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 300_000m);

        Assert.True(result.IsValid);
        Assert.Equal(300_000m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_WhenInactive_ReturnsError()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.IsActive = false;
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 1_000_000m);

        Assert.False(result.IsValid);
        Assert.Contains("không tồn tại hoặc đã bị vô hiệu hóa", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenNotStarted_ReturnsError()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.StartsOn = Today.AddDays(1);
        coupon.EndsOn = Today.AddDays(2);
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 1_000_000m);

        Assert.False(result.IsValid);
        Assert.Contains("chưa có hiệu lực", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_ReturnsError()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.StartsOn = Today.AddDays(-5);
        coupon.EndsOn = Today.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 1_000_000m);

        Assert.False(result.IsValid);
        Assert.Contains("đã hết hạn", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenUsageExhausted_ReturnsError()
    {
        var (db, connection, service) = await CreateAsync(usageLimit: 1);
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.UsedCount = 1;
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 1_000_000m);

        Assert.False(result.IsValid);
        Assert.Contains("đã được sử dụng hết", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenBelowMinOrder_ReturnsError()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var coupon = db.Coupons.Single();
        coupon.MinOrderAmount = 1_000_000m;
        await db.SaveChangesAsync();

        var result = await service.ValidateAsync("SAVE10", 999_999m);

        Assert.False(result.IsValid);
        Assert.Contains("chưa đạt giá trị tối thiểu", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_NormalizesCaseAndWhitespace()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.ValidateAsync(" save10 ", 1_000_000m);

        Assert.True(result.IsValid);
        Assert.Equal("SAVE10", result.Code);
    }

    [Fact]
    public async Task RedeemAsync_Valid_IncrementsUsedCount()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.RedeemAsync("save10", 1_000_000m);

        Assert.True(result.IsValid);
        Assert.Equal(100_000m, result.DiscountAmount);
        Assert.Equal(1, db.Coupons.Single().UsedCount);
    }

    [Fact]
    public async Task RedeemAsync_WhenLimitReached_SecondCallFails()
    {
        var (db, connection, service) = await CreateAsync(usageLimit: 1);
        using var _db = db;
        using var _conn = connection;

        var first = await service.RedeemAsync("SAVE10", 1_000_000m);
        var second = await service.RedeemAsync("SAVE10", 1_000_000m);

        Assert.True(first.IsValid);
        Assert.False(second.IsValid);
        Assert.Contains("đã được sử dụng hết", second.Error);
        Assert.Equal(1, db.Coupons.Single().UsedCount);
    }

    [Fact]
    public async Task CreateAsync_NormalizesCodeAndPersists()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.CreateAsync(Form(" new10 ", 15m));

        Assert.True(result.Success);
        Assert.Equal("Đã tạo mã giảm giá.", result.Message);
        var coupon = db.Coupons.Single(c => c.Code == "NEW10");
        Assert.Equal(DiscountType.Percentage, coupon.DiscountType);
        Assert.Equal(15m, coupon.Value);
        Assert.Equal(5, coupon.UsageLimit);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateCaseInsensitive_Rejects()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await service.CreateAsync(Form("save10"));

        Assert.False(result.Success);
        Assert.Equal("Mã giảm giá đã tồn tại.", result.Message);
        Assert.Equal(1, db.Coupons.Count());
    }

    [Fact]
    public async Task UpdateAsync_ChangesFieldsAndRejectsDuplicate()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        db.Coupons.Add(new Coupon
        {
            Code = "VIP20",
            DiscountType = DiscountType.FixedAmount,
            Value = 20_000m,
            MinOrderAmount = 0m,
            UsageLimit = 5,
            StartsOn = Today.AddDays(-1),
            EndsOn = Today.AddDays(1),
            IsActive = true
        });
        await db.SaveChangesAsync();
        var coupon = db.Coupons.Single(c => c.Code == "SAVE10");

        var update = Form("SAVE10", 20m);
        update.Id = coupon.Id;
        update.UsageLimit = 9;
        var success = await service.UpdateAsync(update);

        Assert.True(success.Success);
        var updated = db.Coupons.Single(c => c.Id == coupon.Id);
        Assert.Equal(20m, updated.Value);
        Assert.Equal(9, updated.UsageLimit);

        var duplicate = Form("vip20", 20m);
        duplicate.Id = coupon.Id;
        var rejected = await service.UpdateAsync(duplicate);

        Assert.False(rejected.Success);
        Assert.Equal("Mã giảm giá đã tồn tại.", rejected.Message);
    }

    [Fact]
    public async Task ToggleActiveAsync_FlipsStatus()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var id = db.Coupons.Single().Id;

        var result = await service.ToggleActiveAsync(id);

        Assert.True(result.Success);
        Assert.False(db.Coupons.Single(c => c.Id == id).IsActive);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCoupon()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;
        var id = db.Coupons.Single().Id;

        var result = await service.DeleteAsync(id);

        Assert.True(result.Success);
        Assert.Null(await db.Coupons.FindAsync(id));
    }
}
