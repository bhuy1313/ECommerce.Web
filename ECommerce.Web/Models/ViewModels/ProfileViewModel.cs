using ECommerce.Web.Models.Entities;

namespace ECommerce.Web.Models.ViewModels;

public class ProfileViewModel
{
    public IReadOnlyList<Address> Addresses { get; set; } = Array.Empty<Address>();
    public AddressFormViewModel NewAddress { get; set; } = new();
}
