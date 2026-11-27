using System.Net;
using System.Net.Sockets;

namespace Jumbo.Api.Services.Payme;

public static class PaymeClientIp
{
    /// <summary>
    /// The address of the caller. The API runs behind our own nginx, so the connection comes from the nginx container (a private address) and
    /// the real address is the LAST entry of X-Forwarded-For (nginx appends what it saw; the entries before it can be written by the caller).
    /// A connection from a public address is taken as it is.
    /// </summary>
    public static IPAddress? Of(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true }) remote = remote.MapToIPv4();
        if (remote is null || !IsInternal(remote)) return remote;

        var last = context.Request.Headers["X-Forwarded-For"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return IPAddress.TryParse(last, out var ip) ? (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip) : remote;
    }

    /// <summary>Loopback, private (the docker network) and link-local addresses: where our own proxy can be.</summary>
    public static bool IsInternal(IPAddress a)
    {
        if (IPAddress.IsLoopback(a)) return true;
        if (a.AddressFamily == AddressFamily.InterNetworkV6) return a.IsIPv6LinkLocal || a.IsIPv6UniqueLocal;
        var b = a.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }
}
