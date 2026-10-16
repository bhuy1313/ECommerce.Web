using ECommerce.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Web.ViewComponents;

/// <summary>Hiển thị số tiền (gốc VNĐ) theo tiền tệ khách đang chọn. Dùng: &lt;vc:money amount="..." /&gt;</summary>
public class MoneyViewComponent : ViewComponent
{
    private readonly ICurrencyService _fx;

    public MoneyViewComponent(ICurrencyService fx)
    {
        _fx = fx;
    }

    public async Task<IViewComponentResult> InvokeAsync(decimal amount)
    {
        return View("Default", await _fx.FormatAsync(amount));
    }
}
