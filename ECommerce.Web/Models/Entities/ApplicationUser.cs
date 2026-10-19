using Microsoft.AspNetCore.Identity;

namespace ECommerce.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public Cart? Cart { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
}