using ECommerce.Web.Data;
using ECommerce.Web.Models.Configuration;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ECommerce.Web.Tests.Services;

public class ShippingCalculatorTests
{
    [Fact]
    public async Task GetActiveMethods_ReturnsOnlyActiveOrdered()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var methods = await calculator.GetActiveMethodsAsync();

        Assert.Equal(new[] { 2, 1 }, methods.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task Quote_ComputesFeeTaxAndTotal()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(1, 1000m);

        Assert.NotNull(quote);
        Assert.Equal(1, quote!.ShippingMethodId);
        Assert.Equal("Tiêu chuẩn", quote.ShippingMethodName);
        Assert.Equal(300m, quote.ShippingFee);
        Assert.Equal(0.1m, quote.TaxRate);
        Assert.Equal(100m, quote.TaxAmount);
        Assert.Equal(1400m, quote.Total);
    }

    [Fact]
    public async Task Quote_RoundsTaxAwayFromZero()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(2, 999m);

        Assert.NotNull(quote);
        Assert.Equal(100m, quote!.TaxAmount);
        Assert.Equal(1099m, quote.Total);
    }

    [Fact]
    public async Task Quote_WhenMethodInactive_ReturnsNull()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(3, 1000m);

        Assert.Null(quote);
    }

    [Fact]
    public async Task Quote_WhenMethodMissing_ReturnsNull()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(999, 1000m);

        Assert.Null(quote);
    }

    [Fact]
    public async Task Quote_WhenIdNull_ReturnsNull()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(null, 1000m);

        Assert.Null(quote);
    }

    [Fact]
    public async Task Quote_ClampsNegativeDiscountedSubtotalToZero()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;
        var calculator = CreateCalculator(db, 0.1m);

        var quote = await calculator.QuoteAsync(1, -500m);

        Assert.NotNull(quote);
        Assert.Equal(0m, quote!.TaxAmount);
        Assert.Equal(300m, quote.Total);
    }

    [Fact]
    public async Task TaxRate_OutsideRange_IsClamped()
    {
        var (db, connection) = await CreateContextAsync();
        using var _ = connection;

        var high = CreateCalculator(db, 1.5m);
        var low = CreateCalculator(db, -0.2m);

        Assert.Equal(1m, high.TaxRate);
        Assert.Equal(0m, low.TaxRate);
        var quote = await high.QuoteAsync(2, 1000m);
        Assert.Equal(1000m, quote!.TaxAmount);
    }

    private static ShippingCalculator CreateCalculator(ApplicationDbContext db, decimal rate) =>
        new(db, Options.Create(new TaxOptions { Rate = rate }));

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.ShippingMethods.AddRange(
            new ShippingMethod { Id = 1, Name = "Tiêu chuẩn", Fee = 300m, IsActive = true, DisplayOrder = 1 },
            new ShippingMethod { Id = 2, Name = "Nhanh", Fee = 0m, IsActive = true, DisplayOrder = 0 },
            new ShippingMethod { Id = 3, Name = "Đã tắt", Fee = 100m, IsActive = false, DisplayOrder = 2 });
        await db.SaveChangesAsync();
        return (db, connection);
    }
}
