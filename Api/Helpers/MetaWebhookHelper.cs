using System.Security.Cryptography;
using System.Text;

namespace CargoInbox.Api.Helpers;

public static class MetaWebhookHelper
{
    public static bool VerifySignature(string rawBody, string appSecret, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedHex = signatureHeader[prefix.Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var actualHex = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHex),
            Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant()));
    }
}
