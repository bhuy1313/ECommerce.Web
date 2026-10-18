using System.Net;
using System.Security.Cryptography;
using System.Text;
using ECommerce.Web.Models.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ECommerce.Web.Tests.Helpers;

/// <summary>
/// Dựng query callback VNPay đã ký, độc lập với implementation production: canonical string
/// (sort key, urlencode key+value, bỏ value rỗng) + HMAC-SHA512 hex với HashSecret.
/// </summary>
public static class VnPaySignatureHelper
{
    public static readonly string WrongSignature = new('a', 128);

    public static string Canonical(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{WebUtility.UrlEncode(parameter.Key)}={WebUtility.UrlEncode(parameter.Value)}"));

    public static string Sign(string hashSecret, IEnumerable<KeyValuePair<string, string>> parameters) =>
        Convert.ToHexString(HMACSHA512.HashData(
                Encoding.UTF8.GetBytes(hashSecret),
                Encoding.UTF8.GetBytes(Canonical(parameters))))
            .ToLowerInvariant();

    public static IQueryCollection BuildCallbackQuery(
        VnPayOptions options,
        string transactionRef,
        decimal amount,
        string responseCode = "00",
        string transactionStatus = "00",
        string? secureHash = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_OrderInfo"] = "Thanh toan don hang",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = options.TmnCode,
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = transactionRef
        };

        var signature = secureHash ?? Sign(options.HashSecret, parameters);

        parameters["vnp_SecureHashType"] = "HmacSHA512";
        parameters["vnp_SecureHash"] = signature;

        return new QueryCollection(parameters.ToDictionary(
            kv => kv.Key,
            kv => new StringValues(kv.Value)));
    }

    public static Dictionary<string, string> ParseQuery(string queryString) =>
        queryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => WebUtility.UrlDecode(parts[0]),
                parts => WebUtility.UrlDecode(parts.Length > 1 ? parts[1] : string.Empty));
}
