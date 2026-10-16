using ECommerce.Web.Models.ViewModels;

namespace ECommerce.Web.Services;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync();
    Task<(bool Success, string Message)> AddItemAsync(int productId, int quantity = 1);
    Task<(bool Success, string Message)> UpdateQuantityAsync(int productId, int quantity);
    Task RemoveItemAsync(int productId);
    Task ClearCartAsync();
    Task MergeGuestCartAsync(string userId);
    Task<int> GetCartItemCountAsync();
}
