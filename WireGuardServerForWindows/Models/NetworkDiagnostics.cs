using System;
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
        public bool HasDns { get; init; }
        public bool HasInternetAccess { get; init; }
    }

    /// <summary>
    /// Read-only checks for the host's upstream path. WinNAT uses the active
    /// default-route adapter rather than a selected ICS adapter.
    /// </summary>
    public static class NetworkDiagnostics
    {
        public static NetworkPathStatus CheckInternetPath()
        {
            NetworkInterface adapter = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsCandidateAdapter)
                .FirstOrDefault(n => n.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

            if (adapter == null)
            {
                return new NetworkPathStatus
                {
                    Routing = "Failed: no connected internet adapter with an IPv4 gateway was found.",
                    Dns = "Not checked",
                    InternetAccess = "Failed: connect an Ethernet, Wi-Fi, or upstream adapter.",
                    Adapter = "No connected upstream adapter"
                };
            }

            IPInterfaceProperties properties = adapter.GetIPProperties();
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
                HasDns = hasDns && dnsLookupSucceeded,
                HasInternetAccess = internetAccess
            };
        }

        private static bool IsCandidateAdapter(NetworkInterface adapter)
        {
            return adapter.OperationalStatus == OperationalStatus.Up
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                && adapter.NetworkInterfaceType != NetworkInterfaceType.Unknown;
        }
    }
}
