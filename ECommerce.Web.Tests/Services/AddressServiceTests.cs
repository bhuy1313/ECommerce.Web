using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using ECommerce.Web.Services;
using ECommerce.Web.Tests.Helpers;
using Microsoft.Data.Sqlite;

namespace ECommerce.Web.Tests.Services;

public class AddressServiceTests
{
    private const string NotFound = "Không tìm thấy địa chỉ.";

    private static async Task<(ApplicationDbContext Db, SqliteConnection Connection, AddressService Service)> CreateAsync()
    {
        var (db, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        db.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@test.local" });
        db.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2@test.local" });
        await db.SaveChangesAsync();
        return (db, connection, new AddressService(db));
    }

    private static Task<(bool Success, string Message)> Add(
        AddressService service, string name = "An", bool makeDefault = false) =>
        service.AddAddressAsync("u1", name, "0901234567", "1 Đường Láng, Hà Nội", makeDefault);

    [Fact]
    public async Task AddAddressAsync_FirstAddress_BecomesDefault()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        var result = await Add(service);

        Assert.True(result.Success);
        Assert.Equal("Đã thêm địa chỉ mới.", result.Message);
        var address = Assert.Single(db.Addresses.Where(a => a.UserId == "u1"));
        Assert.True(address.IsDefault);
    }

    [Fact]
    public async Task AddAddressAsync_SecondAddress_NotDefault()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await Add(service, "An");
        await Add(service, "Bình");

        var addresses = db.Addresses.Where(a => a.UserId == "u1").OrderBy(a => a.Id).ToList();
        Assert.Equal(2, addresses.Count);
        Assert.True(addresses[0].IsDefault);
        Assert.False(addresses[1].IsDefault);
        Assert.Equal(1, addresses.Count(a => a.IsDefault));
    }

    [Fact]
    public async Task AddAddressAsync_MakeDefault_ClearsPreviousDefault()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await Add(service, "An");
        await Add(service, "Bình", makeDefault: true);

        var addresses = db.Addresses.Where(a => a.UserId == "u1").ToList();
        Assert.Equal(2, addresses.Count);
        Assert.Equal(1, addresses.Count(a => a.IsDefault));
        Assert.True(addresses.Single(a => a.RecipientName == "Bình").IsDefault);
    }

    [Fact]
    public async Task GetAddressesAsync_ReturnsOnlyOwnAddresses_DefaultFirst()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await Add(service, "An");
        await Add(service, "Bình");
        await service.AddAddressAsync("u2", "Chi", "0912345678", "2 Lê Lợi, TP.HCM", true);

        var addresses = await service.GetAddressesAsync("u1");

        Assert.Equal(2, addresses.Count);
        Assert.Equal("An", addresses[0].RecipientName);
        Assert.DoesNotContain(addresses, a => a.RecipientName == "Chi");
    }

    [Fact]
    public async Task SetDefaultAddressAsync_WhenOwned_SwitchesDefault()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await Add(service, "An");
        await Add(service, "Bình");
        var second = db.Addresses.Single(a => a.RecipientName == "Bình");

        var result = await service.SetDefaultAddressAsync("u1", second.Id);

        Assert.True(result.Success);
        Assert.Equal("Đã đặt làm địa chỉ mặc định.", result.Message);
        var addresses = db.Addresses.Where(a => a.UserId == "u1").ToList();
        Assert.Equal(1, addresses.Count(a => a.IsDefault));
        Assert.True(addresses.Single(a => a.RecipientName == "Bình").IsDefault);
    }

    [Fact]
    public async Task SetDefaultAddressAsync_WhenForeignOrMissing_Rejects()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await service.AddAddressAsync("u2", "Chi", "0912345678", "2 Lê Lợi, TP.HCM", true);
        var foreign = db.Addresses.Single(a => a.RecipientName == "Chi");

        var foreignResult = await service.SetDefaultAddressAsync("u1", foreign.Id);
        var missingResult = await service.SetDefaultAddressAsync("u1", 99999);

        Assert.False(foreignResult.Success);
        Assert.Equal(NotFound, foreignResult.Message);
        Assert.False(missingResult.Success);
        Assert.Equal(NotFound, missingResult.Message);
        Assert.True(db.Addresses.Single(a => a.Id == foreign.Id).IsDefault);
    }

    [Fact]
    public async Task DeleteAddressAsync_WhenDefault_PromotesOldestRemaining()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await Add(service, "An");
        await Add(service, "Bình");
        await Add(service, "Chi");
        var first = db.Addresses.Single(a => a.RecipientName == "An");

        var result = await service.DeleteAddressAsync("u1", first.Id);

        Assert.True(result.Success);
        var addresses = db.Addresses.Where(a => a.UserId == "u1").ToList();
        Assert.Equal(2, addresses.Count);
        Assert.True(addresses.Single(a => a.RecipientName == "Bình").IsDefault);
        Assert.Equal(1, addresses.Count(a => a.IsDefault));
    }

    [Fact]
    public async Task DeleteAddressAsync_WhenForeign_KeepsRow()
    {
        var (db, connection, service) = await CreateAsync();
        using var _db = db;
        using var _conn = connection;

        await service.AddAddressAsync("u2", "Chi", "0912345678", "2 Lê Lợi, TP.HCM", true);
        var foreign = db.Addresses.Single(a => a.RecipientName == "Chi");

        var result = await service.DeleteAddressAsync("u1", foreign.Id);

        Assert.False(result.Success);
        Assert.Equal(NotFound, result.Message);
        Assert.NotNull(db.Addresses.SingleOrDefault(a => a.Id == foreign.Id));
    }
}
