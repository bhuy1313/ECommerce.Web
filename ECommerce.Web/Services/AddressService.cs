using ECommerce.Web.Data;
using ECommerce.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Web.Services;

public class AddressService : IAddressService
{
    private const string NotFound = "Không tìm thấy địa chỉ.";

    private readonly ApplicationDbContext _db;

    public AddressService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Address>> GetAddressesAsync(string userId)
    {
        return await _db.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Id)
            .ToListAsync();
    }

    public Task<Address?> GetDefaultAddressAsync(string userId)
    {
        return _db.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
    }

    public async Task<(bool Success, string Message)> AddAddressAsync(
        string userId, string recipientName, string recipientPhone, string addressLine, bool makeDefault)
    {
        var hasAny = await _db.Addresses.AnyAsync(a => a.UserId == userId);
        makeDefault = makeDefault || !hasAny;

        if (makeDefault)
        {
            await ClearDefaultsAsync(userId);
        }

        _db.Addresses.Add(new Address
        {
            UserId = userId,
            RecipientName = recipientName.Trim(),
            RecipientPhone = recipientPhone.Trim(),
            AddressLine = addressLine.Trim(),
            IsDefault = makeDefault
        });
        await _db.SaveChangesAsync();

        return (true, "Đã thêm địa chỉ mới.");
    }

    public async Task<(bool Success, string Message)> SetDefaultAddressAsync(string userId, int addressId)
    {
        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        if (address is null)
        {
            return (false, NotFound);
        }

        if (address.IsDefault)
        {
            return (true, "Đã đặt làm địa chỉ mặc định.");
        }

        await ClearDefaultsAsync(userId);
        address.IsDefault = true;
        await _db.SaveChangesAsync();

        return (true, "Đã đặt làm địa chỉ mặc định.");
    }

    public async Task<(bool Success, string Message)> DeleteAddressAsync(string userId, int addressId)
    {
        var address = await _db.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);

        if (address is null)
        {
            return (false, NotFound);
        }

        var wasDefault = address.IsDefault;
        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync();

        if (wasDefault)
        {
            var next = await _db.Addresses
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();

            if (next is not null)
            {
                next.IsDefault = true;
                await _db.SaveChangesAsync();
            }
        }

        return (true, "Đã xóa địa chỉ.");
    }

    private async Task ClearDefaultsAsync(string userId)
    {
        var defaults = await _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync();

        foreach (var address in defaults)
        {
            address.IsDefault = false;
        }
    }
}
