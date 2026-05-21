using System.Security.Cryptography;
using System.Text;
using AedBackend.Infrastructure.WeChat;

namespace AedBackend.Api.Endpoints;

internal static class EndpointSecurity
{
    public const string AdminKeyHeader = "X-Aed-Admin-Key";

    public static bool HasAdminKey(WeChatOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.AdminKey);
    }

    public static bool IsAdminAuthorized(HttpRequest request, WeChatOptions options)
    {
        if (!request.Headers.TryGetValue(AdminKeyHeader, out var values))
        {
            return false;
        }

        return FixedTimeEquals(values.ToString(), options.AdminKey);
    }

    public static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
