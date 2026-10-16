using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Web.Services;

/// <summary>
/// Quy đổi display-only qua Frankfurter API (free, không key), cache 12h.
/// Giá gốc và nghiệp vụ luôn VNĐ; lỗi tỉ giá → hiển thị VNĐ.
/// </summary>
public class CurrencyService : ICurrencyService
{
    public const string DefaultApiBaseUrl = "https://api.frankfurter.dev/";
    private const string RatesCacheKey = "fx_rates_vnd";
    private const int DefaultCacheHours = 12;

    private static readonly CurrencyOption[] SupportedCurrencies =
    [
        new("VND", "Việt Nam Đồng", "vi-VN", 0, "đ", false),
        new("USD", "US Dollar", "en-US", 2, "$", true),
        new("EUR", "Euro", "de-DE", 2, "€", false),
        new("GBP", "British Pound", "en-GB", 2, "£", true),
        new("JPY", "Japanese Yen", "ja-JP", 0, "¥", true),
        new("KRW", "Korean Won", "ko-KR", 0, "₩", true),
        new("CNY", "Chinese Yuan", "zh-CN", 2, "CN¥", true),
        new("SGD", "Singapore Dollar", "en-SG", 2, "S$", true),
    ];

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrencyService> _logger;
    private readonly int _cacheHours;

    public CurrencyService(
        HttpClient http,
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrencyService> logger,
        IConfiguration configuration)
    {
        _http = http;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _cacheHours = configuration.GetValue<int?>("Currency:CacheHours") ?? DefaultCacheHours;
    }

    public string CookieName => "technova-currency";

    public IReadOnlyList<CurrencyOption> Supported => SupportedCurrencies;

    public bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        SupportedCurrencies.Any(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public string CurrentCode
    {
        get
        {
            var code = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
            if (IsSupported(code))
            {
                return code!.Trim().ToUpperInvariant();
            }
            return "VND";
        }
    }

    public async Task<string> FormatAsync(decimal amountVnd)
    {
        var code = CurrentCode;
        if (code == "VND")
        {
            return FormatVnd(amountVnd);
        }

        var option = SupportedCurrencies.First(c => c.Code == code);
        var rates = await GetRatesAsync();
        if (rates == null || !rates.TryGetValue(code, out var rate) || rate <= 0)
        {
            return FormatVnd(amountVnd);
        }

        var converted = amountVnd * rate;
        var culture = CultureInfo.GetCultureInfo(option.Culture);
        var number = converted.ToString("N" + option.Decimals, culture);
        return option.SymbolFirst ? $"{option.Symbol}{number}" : $"{number} {option.Symbol}";
    }

    /// <summary>Giữ nguyên format VNĐ cũ của site (invariant + "đ").</summary>
    private static string FormatVnd(decimal amountVnd) =>
        $"{amountVnd.ToString("N0", CultureInfo.InvariantCulture)} đ";

    /// <summary>Tỉ giá VND → các ngoại tệ (1 VNĐ = rate ngoại tệ), cache theo giờ.</summary>
    private async Task<Dictionary<string, decimal>?> GetRatesAsync()
    {
        if (_cache.TryGetValue(RatesCacheKey, out Dictionary<string, decimal>? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var quotes = string.Join(",", SupportedCurrencies.Where(c => c.Code != "VND").Select(c => c.Code));
            using var response = await _http.GetAsync($"v2/rates?base=VND&quotes={quotes}");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var rows = await JsonSerializer.DeserializeAsync<List<FrankfurterRate>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rows == null || rows.Count == 0)
            {
                return null;
            }

            var rates = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Quote) && r.Rate > 0)
                .ToDictionary(r => r.Quote!.ToUpperInvariant(), r => r.Rate);

            _cache.Set(RatesCacheKey, rates, TimeSpan.FromHours(_cacheHours));
            return rates;
        }
        catch (Exception ex)
        {
            // Display-only: lỗi tỉ giá không được làm sập trang.
            _logger.LogWarning(ex, "Không lấy được tỉ giá Frankfurter, fallback hiển thị VNĐ.");
            return null;
        }
    }

    private sealed class FrankfurterRate
    {
        public string? Quote { get; set; }
        public decimal Rate { get; set; }
    }
}
