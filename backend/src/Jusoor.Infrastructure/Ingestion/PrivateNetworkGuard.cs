using System.Net;
using System.Net.Sockets;

namespace Jusoor.Infrastructure.Ingestion;

/// <summary>
/// Threat model (per §9): a Source.FeedUrl is operator-configured, not
/// end-user input directly — but a compromised or careless source
/// configuration (or, later, any feature that lets an editor add a source
/// URL) could point at internal infrastructure, cloud metadata endpoints, or
/// loopback services. This guard is the single place that decides "is this
/// IP safe to connect to", checked against the ACTUAL resolved address the
/// socket is about to connect to (see SsrfSafeSourceFetcher's
/// ConnectCallback) — not just the hostname string, which is what makes
/// this resistant to DNS-rebinding: an attacker can't get a "validated"
/// hostname to resolve to something different by the time the connection
/// is actually opened, because we resolve once and connect to the address
/// we validated, rather than re-resolving at connect time.
/// </summary>
public static class PrivateNetworkGuard
{
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsPublicIPv4(address);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsPublicIPv6(address);
        }

        // Unknown address family — fail closed, not open.
        return false;
    }

    private static bool IsPublicIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (IPAddress.IsLoopback(address)) return false;             // 127.0.0.0/8
        if (bytes[0] == 0) return false;                              // 0.0.0.0/8
        if (bytes[0] == 10) return false;                             // 10.0.0.0/8
        if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return false; // 172.16.0.0/12
        if (bytes[0] == 192 && bytes[1] == 168) return false;         // 192.168.0.0/16
        if (bytes[0] == 169 && bytes[1] == 254) return false;         // 169.254.0.0/16 — INCLUDES 169.254.169.254 cloud metadata
        if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return false; // 100.64.0.0/10 — CGNAT
        if (bytes[0] >= 224) return false;                            // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved

        return true;
    }

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false; // ::1
        if (address.IsIPv6LinkLocal) return false;        // fe80::/10
        if (address.IsIPv6SiteLocal) return false;        // fec0::/10 (deprecated but still rejected)
        if (address.IsIPv6Multicast) return false;

        var bytes = address.GetAddressBytes();
        if ((bytes[0] & 0xFE) == 0xFC) return false; // fc00::/7 — Unique Local Addresses (IPv6 private range)
        if (bytes.All(b => b == 0)) return false;    // :: unspecified

        return true;
    }
}
