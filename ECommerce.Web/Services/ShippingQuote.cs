namespace ECommerce.Web.Services;

public sealed record ShippingQuote(
    int ShippingMethodId,
    string ShippingMethodName,
    decimal ShippingFee,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total);
