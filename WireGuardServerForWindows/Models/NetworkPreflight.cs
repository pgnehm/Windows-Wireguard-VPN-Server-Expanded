using System;
using System.Collections.Generic;
using System.Linq;

namespace WireGuardServerForWindows.Models
{
    /// <summary>
    /// A pure, testable summary of whether the host is ready to provide client
    /// internet access. A missing upstream route is a warning rather than a
    /// configuration failure because adapters commonly appear after boot.
    /// </summary>
    public sealed class NetworkPreflightResult
    {
        public bool CanConfigure { get; init; }
        public bool IsHealthy { get; init; }
        public IReadOnlyList<string> BlockingIssues { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public string Summary
        {
            get
            {
                IEnumerable<string> messages = BlockingIssues.Concat(Warnings);
                return string.Join(" ", messages);
            }
        }
    }

    public static class NetworkPreflight
    {
        public static NetworkPreflightResult Evaluate(
            string wireGuardNetwork,
            NetworkPathStatus path,
            bool natConfigured)
        {
            var blockingIssues = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(wireGuardNetwork)
                || !System.Net.IPNetwork.TryParse(wireGuardNetwork, out var network)
                || network.BaseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                blockingIssues.Add("The WireGuard network must be a valid IPv4 CIDR network.");
            }

            if (!natConfigured)
            {
                blockingIssues.Add("Windows NAT is not configured for the WireGuard network.");
            }

            if (path == null || !path.HasConnectedAdapter)
            {
                warnings.Add("No connected upstream adapter was found. Connect Ethernet or Wi-Fi for client internet access.");
            }
            else if (!path.HasDefaultRoute)
            {
                warnings.Add("The upstream adapter is connected, but Windows has no IPv4 default route.");
            }

            if (path != null && path.HasDefaultRoute && !path.HasDns)
            {
                warnings.Add("The upstream adapter does not have working DNS.");
            }

            if (path != null && path.HasDefaultRoute && !path.HasInternetAccess)
            {
                warnings.Add("The host cannot currently reach the internet over HTTPS.");
            }

            return new NetworkPreflightResult
            {
                CanConfigure = blockingIssues.Count == 0,
                IsHealthy = blockingIssues.Count == 0 && warnings.Count == 0,
                BlockingIssues = blockingIssues,
                Warnings = warnings
            };
        }
    }
}
