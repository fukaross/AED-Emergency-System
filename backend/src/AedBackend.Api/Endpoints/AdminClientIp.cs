using System.Net;

namespace AedBackend.Api.Endpoints;

internal static class AdminClientIp
{
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string RealIpHeader = "X-Real-IP";

    public static string? Resolve(HttpContext context)
    {
        return ResolveForwardedFor(context)
            ?? ResolveSingleHeader(context, RealIpHeader)
            ?? Normalize(context.Connection.RemoteIpAddress?.ToString());
    }

    private static string? ResolveForwardedFor(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(ForwardedForHeader, out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            var rawValue = value;
            if (rawValue is null) continue;

            foreach (var candidate in rawValue.Split(','))
            {
                var normalized = Normalize(candidate);
                if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
            }
        }

        return null;
    }

    private static string? ResolveSingleHeader(HttpContext context, string headerName)
    {
        if (!context.Request.Headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        return values
            .Select(Normalize)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? Normalize(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var parsed))
        {
            return null;
        }

        return Normalize(parsed);
    }

    private static string Normalize(IPAddress ipAddress)
    {
        return ipAddress.IsIPv4MappedToIPv6
            ? ipAddress.MapToIPv4().ToString()
            : ipAddress.ToString();
    }
}
