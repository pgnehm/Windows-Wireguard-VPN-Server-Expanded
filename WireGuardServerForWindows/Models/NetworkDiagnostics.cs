using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace WireGuardServerForWindows.Models
{
    public sealed class NetworkPathStatus
    {
        public string Routing { get; init; } = "Unknown";
        public string Dns { get; init; } = "Unknown";
        public string InternetAccess { get; init; } = "Unknown";
        public string Adapter { get; init; } = "Unknown";
        public bool HasConnectedAdapter { get; init; }
        public bool HasDefaultRoute { get; init; }
        public bool HasDns { get; init; }
        public bool HasInternetAccess { get; init; }
        public int DnsServerCount { get; init; }
        public DateTimeOffset CheckedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Read-only checks for the host's upstream path. WinNAT uses the active
    /// default-route adapter rather than a selected ICS adapter.
    /// </summary>
    public static class NetworkDiagnostics
    {
        public static NetworkPathStatus CheckInternetPath()
        {
            List<NetworkInterface> adapters;
            try
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(IsCandidateAdapter)
                    .ToList();
            }
            catch (Exception exception)
            {
                return new NetworkPathStatus
                {
                    Routing = $"Failed: Windows could not enumerate network adapters ({exception.Message})",
                    Dns = "Not checked",
                    InternetAccess = "Not checked",
                    Adapter = "Adapter enumeration failed"
                };
            }

            NetworkInterface connectedAdapter = adapters.FirstOrDefault();
            NetworkInterface adapter = adapters.FirstOrDefault(HasIpv4Gateway);

            if (adapter == null)
            {
                return new NetworkPathStatus
                {
                    Routing = connectedAdapter == null
                        ? "Failed: no connected internet adapter was found."
                        : "Failed: a connected adapter has no IPv4 default gateway.",
                    Dns = "Not checked",
                    InternetAccess = "Failed: establish an IPv4 default route before starting the VPN.",
                    Adapter = connectedAdapter == null
                        ? "No connected upstream adapter"
                        : $"{connectedAdapter.Name} ({connectedAdapter.NetworkInterfaceType})",
                    HasConnectedAdapter = connectedAdapter != null,
                    CheckedAtUtc = DateTimeOffset.UtcNow
                };
            }

            IPInterfaceProperties properties;
            try
            {
                properties = adapter.GetIPProperties();
            }
            catch (Exception exception)
            {
                return new NetworkPathStatus
                {
                    Routing = "Failed: the upstream adapter properties could not be read.",
                    Dns = "Not checked",
                    InternetAccess = "Not checked",
                    Adapter = $"{adapter.Name} ({adapter.NetworkInterfaceType}): {exception.Message}",
                    HasConnectedAdapter = true,
                    HasDefaultRoute = true,
                    CheckedAtUtc = DateTimeOffset.UtcNow
                };
            }

            bool hasDns = properties.DnsAddresses.Any();
            string dnsStatus = hasDns ? $"Configured ({properties.DnsAddresses.Count} server(s))" : "Failed: no DNS server is configured.";
            bool dnsLookupSucceeded = false;

            if (hasDns)
            {
                try
                {
                    dnsLookupSucceeded = Dns.GetHostAddresses("example.com").Length > 0;
                    if (!dnsLookupSucceeded)
                    {
                        dnsStatus = "Failed: DNS lookup returned no addresses.";
                    }
                }
                catch (Exception exception)
                {
                    dnsStatus = $"Failed: DNS lookup failed ({exception.Message})";
                }
            }

            bool internetAccess = false;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using HttpResponseMessage response = client.GetAsync("https://www.msftconnecttest.com/connecttest.txt")
                    .GetAwaiter()
                    .GetResult();
                internetAccess = (int)response.StatusCode >= 200 && (int)response.StatusCode < 500;
            }
            catch
            {
                // The result below is intentionally actionable rather than exposing
                // an exception from a transient connectivity check.
            }

            return new NetworkPathStatus
            {
                Routing = "OK: an upstream adapter has a default IPv4 gateway.",
                Dns = dnsLookupSucceeded ? $"OK: {dnsStatus}" : dnsStatus,
                InternetAccess = internetAccess
                    ? "OK: an HTTPS connectivity check succeeded."
                    : "Failed: the host could not reach the internet over HTTPS.",
                Adapter = $"{adapter.Name} ({adapter.NetworkInterfaceType})",
                HasConnectedAdapter = true,
                HasDefaultRoute = true,
                HasDns = hasDns && dnsLookupSucceeded,
                HasInternetAccess = internetAccess,
                DnsServerCount = properties.DnsAddresses.Count,
                CheckedAtUtc = DateTimeOffset.UtcNow
            };
        }

        private static bool IsCandidateAdapter(NetworkInterface adapter)
        {
            return adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Unknown;
        }

        private static bool HasIpv4Gateway(NetworkInterface adapter)
        {
            try
            {
                return adapter.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }
            catch
            {
                return false;
            }
        }
    }
}
