namespace ECommerce.Web.Services;

/// <summary>
/// Quy đổi tiền tệ display-only: giá gốc trong DB và mọi nghiệp vụ
/// (giỏ, đơn hàng, thanh toán) luôn là VNĐ; service này chỉ phục vụ hiển thị.
/// </summary>
public interface ICurrencyService
{
    /// <summary>Tên cookie lưu lựa chọn tiền tệ.</summary>
    string CookieName { get; }

    /// <summary>Mã tiền tệ đang chọn (cookie), fallback VND khi không hợp lệ.</summary>
    string CurrentCode { get; }

    /// <summary>Danh sách tiền tệ được hỗ trợ.</summary>
    IReadOnlyList<CurrencyOption> Supported { get; }

    /// <summary>Kiểm tra mã tiền tệ có được hỗ trợ không.</summary>
    bool IsSupported(string? code);

    /// <summary>
    /// Format số tiền (đơn vị gốc VNĐ) theo tiền tệ đang chọn.
    /// Lỗi tỉ giá → fallback chuỗi VNĐ, không bao giờ throw.
    /// </summary>
    Task<string> FormatAsync(decimal amountVnd);
}

/// <summary>Một đơn vị tiền tệ hiển thị được.</summary>
/// <param name="Code">Mã ISO (VND, USD...).</param>
/// <param name="Label">Tên gợi nhớ trong dropdown.</param>
/// <param name="Culture">Culture dùng để format số.</param>
/// <param name="Decimals">Số chữ số thập phân (VND/JPY/KRW = 0).</param>
/// <param name="Symbol">Ký hiệu tiền tệ.</param>
/// <param name="SymbolFirst">True: ký hiệu đứng trước số ($1,000), false: sau (1.000 €).</param>
public sealed record CurrencyOption(
    string Code,
    string Label,
    string Culture,
    int Decimals,
    string Symbol,
    bool SymbolFirst);
