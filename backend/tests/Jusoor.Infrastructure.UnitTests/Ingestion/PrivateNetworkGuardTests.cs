using System.Net;
using FluentAssertions;
using Jusoor.Infrastructure.Ingestion;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Ingestion;

public class PrivateNetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]           // loopback
    [InlineData("10.0.0.1")]            // RFC1918
    [InlineData("172.16.0.1")]          // RFC1918
    [InlineData("172.31.255.255")]      // RFC1918 upper bound
    [InlineData("192.168.1.1")]         // RFC1918
    [InlineData("169.254.169.254")]     // cloud metadata endpoint — the specific SSRF target this exists to block
    [InlineData("169.254.0.1")]         // link-local generally
    [InlineData("100.64.0.1")]          // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("240.0.0.1")]           // reserved
    [InlineData("224.0.0.1")]           // multicast
    public void IsPublicAddress_should_reject_private_and_reserved_ipv4(string ip)
    {
        PrivateNetworkGuard.IsPublicAddress(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    public void IsPublicAddress_should_accept_ordinary_public_ipv4(string ip)
    {
        PrivateNetworkGuard.IsPublicAddress(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("::1")]                  // loopback
    [InlineData("fe80::1")]              // link-local
    [InlineData("fc00::1")]              // unique local (private)
    [InlineData("fd12:3456:789a::1")]    // unique local (private)
    [InlineData("::")]                   // unspecified
    public void IsPublicAddress_should_reject_private_ipv6(string ip)
    {
        PrivateNetworkGuard.IsPublicAddress(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Fact]
    public void IsPublicAddress_should_accept_ordinary_public_ipv6()
    {
        PrivateNetworkGuard.IsPublicAddress(IPAddress.Parse("2001:4860:4860::8888")).Should().BeTrue();
    }

    [Fact]
    public void IsPublicAddress_should_unwrap_ipv4_mapped_ipv6_and_apply_ipv4_rules()
    {
        // ::ffff:169.254.169.254 — a v4-mapped-in-v6 form of the cloud
        // metadata address. If this weren't unwrapped, the naive IPv6 checks
        // alone would not catch it — exactly the kind of bypass this guard
        // needs to close.
        var mapped = IPAddress.Parse("::ffff:169.254.169.254");
        PrivateNetworkGuard.IsPublicAddress(mapped).Should().BeFalse();
    }
}
