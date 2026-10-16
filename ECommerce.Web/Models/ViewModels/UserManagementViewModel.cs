namespace ECommerce.Web.Models.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public int OrderCount { get; set; }
    public bool IsCurrentAdmin { get; set; }
}

public class EditUserRolesViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<RoleSelectionViewModel> Roles { get; set; } = new();
}

public class RoleSelectionViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
