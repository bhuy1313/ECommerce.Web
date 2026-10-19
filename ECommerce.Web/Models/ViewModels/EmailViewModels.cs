namespace ECommerce.Web.Models.ViewModels;

public sealed record EmailLinkViewModel(string Url);

public sealed record OrderConfirmationEmailViewModel(
    ECommerce.Web.Models.Entities.Order Order,
    string OrderUrl);
