using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Jusoor.Infrastructure.Ingestion;

/// <summary>
/// Provides the SocketsHttpHandler.ConnectCallback used by
/// SsrfSafeSourceFetcher. This is where DNS-rebinding resistance actually
/// happens: we resolve the hostname exactly once, validate every resolved
/// address, then open the socket directly against the validated IP — the
/// runtime never gets a chance to re-resolve the hostname (which is how a
/// DNS-rebinding attack works: pass validation with a safe IP, then have
/// the second lookup at connect time return an internal IP instead).
/// </summary>
public static class SafeSocketConnector
{
    public static async ValueTask<System.IO.Stream> ConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SsrfViolationException($"DNS resolution failed for '{host}'.", ex);
        }

        var safeAddress = addresses.FirstOrDefault(PrivateNetworkGuard.IsPublicAddress);
        if (safeAddress is null)
        {
            throw new SsrfViolationException(
                $"'{host}' resolved only to non-public/reserved addresses ({string.Join(", ", addresses.Select(a => a.ToString()))}); refusing to connect.");
        }

        var socket = new Socket(safeAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            // Connect to the address we just validated — NOT back to the
            // hostname, which is the part that closes the DNS-rebinding
            // window.
            await socket.ConnectAsync(safeAddress, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
