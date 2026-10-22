namespace ECommerce.Web.Models.Configuration;

public class TaxOptions
{
    public const string SectionName = "Tax";

    public decimal Rate { get; set; } = 0.10m;
}
