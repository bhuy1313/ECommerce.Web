using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Services;

public interface IAddressService
{
    Task<IReadOnlyList<Address>> GetAddressesAsync(string userId);
    Task<Address?> GetDefaultAddressAsync(string userId);
    Task<(bool Success, string Message)> AddAddressAsync(
        string userId, string recipientName, string recipientPhone, string addressLine, bool makeDefault);
    Task<(bool Success, string Message)> SetDefaultAddressAsync(string userId, int addressId);
    Task<(bool Success, string Message)> DeleteAddressAsync(string userId, int addressId);
}
