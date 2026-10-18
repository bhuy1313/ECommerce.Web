using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Web.Models.Configuration;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Web.Services.Payments;

/// <summary>
/// Cổng thanh toán VNPay theo API 2.1.0: tham số sắp xếp theo key, URL-encode key và value,
/// chữ ký HMAC-SHA512 (hex) trên canonical string với <c>HashSecret</c>.
/// </summary>
public class VnPayGateway : IPaymentGateway
{
    private const string Version = "2.1.0";
    private const string Command = "pay";
    private const string CurrCode = "VND";
    private const string OrderType = "other";
    private const string Locale = "vn";
    private const string SecureHashKey = "vnp_SecureHash";
    private const string SecureHashTypeKey = "vnp_SecureHashType";
    private const string SuccessCode = "00";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly VnPayOptions _options;

    public VnPayGateway(VnPayOptions options) => _options = options;

    public string Provider => "VnPay";

    public Task<PaymentInitResult> CreatePaymentAsync(PaymentRequest request, string returnUrl, string ipAddress)
    {
        var transactionRef = BuildTransactionRef(request.OrderId);
        var createDate = DateTimeOffset.UtcNow.ToOffset(VietnamOffset);
        var parameters = new Dictionary<string, string>
        {
            ["vnp_Version"] = Version,
            ["vnp_Command"] = Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Locale"] = Locale,
            ["vnp_CurrCode"] = CurrCode,
            ["vnp_TxnRef"] = transactionRef,
            ["vnp_OrderInfo"] = ToAscii(request.OrderInfo),
            ["vnp_OrderType"] = OrderType,
            ["vnp_Amount"] = ToVnPayAmount(request.Amount),
            ["vnp_ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? _options.ReturnUrl : returnUrl,
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_CreateDate"] = ToVnPayDate(createDate),
            ["vnp_ExpireDate"] = ToVnPayDate(createDate.AddMinutes(15))
        };

        var redirectUrl = $"{_options.BaseUrl}?{BuildCanonicalString(parameters)}&{SecureHashKey}={Sign(parameters)}";

        return Task.FromResult(new PaymentInitResult
        {
            Success = true,
            RedirectUrl = redirectUrl,
            TransactionRef = transactionRef
        });
    }

    public Task<PaymentCallbackResult> VerifyCallbackAsync(IQueryCollection query)
    {
        var parameters = query
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                && kv.Key is not (SecureHashKey or SecureHashTypeKey))
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var isValidSignature = VerifySignature(parameters, query[SecureHashKey].ToString());
        var transactionRef = query["vnp_TxnRef"].ToString();

        return Task.FromResult(new PaymentCallbackResult
        {
            IsValidSignature = isValidSignature,
            Success = isValidSignature
                && query["vnp_ResponseCode"].ToString() == SuccessCode
                && query["vnp_TransactionStatus"].ToString() == SuccessCode,
            TransactionRef = transactionRef,
            Amount = ToDecimalAmount(query["vnp_Amount"].ToString()),
            OrderId = TryParseOrderId(transactionRef)
        });
    }

    private string Sign(IEnumerable<KeyValuePair<string, string>> parameters) =>
        Convert.ToHexString(HMACSHA512.HashData(
                Encoding.UTF8.GetBytes(_options.HashSecret),
                Encoding.UTF8.GetBytes(BuildCanonicalString(parameters))))
            .ToLowerInvariant();

    private bool VerifySignature(IReadOnlyDictionary<string, string> parameters, string receivedHash)
    {
        if (string.IsNullOrEmpty(receivedHash))
        {
            return false;
        }

        byte[] received;
        try
        {
            received = Convert.FromHexString(receivedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(_options.HashSecret),
            Encoding.UTF8.GetBytes(BuildCanonicalString(parameters)));

        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    private static string BuildCanonicalString(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{WebUtility.UrlEncode(parameter.Key)}={WebUtility.UrlEncode(parameter.Value)}"));

    private static string BuildTransactionRef(int orderId) =>
        $"{orderId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    private static int? TryParseOrderId(string transactionRef)
    {
        var separator = transactionRef.IndexOf('-', StringComparison.Ordinal);
        var orderIdPart = separator < 0 ? transactionRef : transactionRef[..separator];

        return int.TryParse(orderIdPart, NumberStyles.None, CultureInfo.InvariantCulture, out var orderId)
            ? orderId
            : null;
    }

    private static string ToVnPayAmount(decimal amount) =>
        ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture);

    private static decimal ToDecimalAmount(string rawAmount) =>
        long.TryParse(rawAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            ? amount / 100m
            : 0m;

    private static string ToVnPayDate(DateTimeOffset value) =>
        value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string ToAscii(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character
            });
        }

        return string.Join(' ', builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
