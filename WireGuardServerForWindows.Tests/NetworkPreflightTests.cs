using FluentAssertions;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class NetworkPreflightTests
    {
        [Fact]
        public void ShouldReportHealthyWhenNatAndUpstreamPathAreReady()
        {
            NetworkPreflightResult result = NetworkPreflight.Evaluate(
                "10.253.0.0/24",
                new NetworkPathStatus
                {
                    HasConnectedAdapter = true,
                    HasDefaultRoute = true,
                    HasDns = true,
                    HasInternetAccess = true
                },
                natConfigured: true);

            result.CanConfigure.Should().BeTrue();
            result.IsHealthy.Should().BeTrue();
            result.BlockingIssues.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
        }

        [Fact]
        public void ShouldWarnWhenAdapterIsConnectedWithoutDefaultRoute()
        {
            NetworkPreflightResult result = NetworkPreflight.Evaluate(
                "10.253.0.0/24",
                new NetworkPathStatus
                {
                    HasConnectedAdapter = true,
                    HasDefaultRoute = false
                },
                natConfigured: true);

            result.CanConfigure.Should().BeTrue();
            result.IsHealthy.Should().BeFalse();
            result.Summary.Should().Contain("default route");
        }

        [Fact]
        public void ShouldBlockAnInvalidNetworkOrMissingNat()
        {
            NetworkPreflightResult result = NetworkPreflight.Evaluate(
                "not-a-network",
                new NetworkPathStatus(),
                natConfigured: false);

            result.CanConfigure.Should().BeFalse();
            result.IsHealthy.Should().BeFalse();
            result.BlockingIssues.Should().HaveCount(2);
            result.Summary.Should().Contain("valid IPv4 CIDR");
            result.Summary.Should().Contain("Windows NAT is not configured");
        }
    }
}
