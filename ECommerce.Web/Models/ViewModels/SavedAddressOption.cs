namespace ECommerce.Web.Models.ViewModels;

public sealed record SavedAddressOption(
    int Id, string Display, string RecipientName, string RecipientPhone, string AddressLine);
