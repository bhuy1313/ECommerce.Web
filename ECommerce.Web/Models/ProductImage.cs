namespace ECommerce.Web.Models;

/// <summary>Đường dẫn ảnh hiển thị của sản phẩm, kèm ảnh thay thế khi sản phẩm chưa có ảnh.</summary>
public static class ProductImage
{
    /// <summary>Ảnh hiển thị khi <c>Product.ImageUrl</c> trống.</summary>
    public const string Placeholder = "/img/product-placeholder.svg";

    /// <summary>Trả về <see cref="Placeholder"/> nếu <paramref name="imageUrl"/> rỗng, ngược lại trả về URL đã cắt khoảng trắng.</summary>
    public static string Resolve(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? Placeholder : imageUrl.Trim();
}
