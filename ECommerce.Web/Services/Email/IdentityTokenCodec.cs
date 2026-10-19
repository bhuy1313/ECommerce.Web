using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace ECommerce.Web.Services.Email;

public static class IdentityTokenCodec
{
    public static string Encode(string token)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public static bool TryDecode(string? code, out string token)
    {
        token = string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            return token.Length > 0;
        }
        catch (FormatException)
        {
            token = string.Empty;
            return false;
        }
    }
}
