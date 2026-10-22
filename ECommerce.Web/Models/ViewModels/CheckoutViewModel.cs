using System.ComponentModel.DataAnnotations;
using ECommerce.Web.Models.Entities.Enums;

namespace ECommerce.Web.Models.ViewModels;

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận.")]
    [StringLength(100)]
    [Display(Name = "Người nhận")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(15)]
    [Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
    [StringLength(300)]
    [Display(Name = "Địa chỉ giao hàng")]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "Vui lòng chọn phương thức thanh toán hợp lệ.")]
    [Display(Name = "Phương thức thanh toán")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cod;

    public int? SavedAddressId { get; set; }

    [StringLength(50)]
    public string? CouponCode { get; set; }

    public decimal DiscountAmount { get; set; }

    public string? CouponError { get; set; }

    public string? CouponDescription { get; set; }

    public int? ShippingMethodId { get; set; }

    public IReadOnlyList<ShippingMethodOption> ShippingMethods { get; set; } = Array.Empty<ShippingMethodOption>();

    public decimal ShippingFee { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public string? ShippingError { get; set; }

    public decimal PayableTotal => Math.Max(0m, Cart.TotalAmount - DiscountAmount + ShippingFee + TaxAmount);

    public IReadOnlyList<SavedAddressOption> SavedAddresses { get; set; } = Array.Empty<SavedAddressOption>();

    public CartViewModel Cart { get; set; } = new();
}
