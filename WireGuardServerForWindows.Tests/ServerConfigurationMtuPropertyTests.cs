using FluentAssertions;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class ServerConfigurationMtuPropertyTests
    {
        [Theory]
        [InlineData("1280")]
        [InlineData("1420")]
        [InlineData("1500")]
        [InlineData("65535")]
        public void ShouldAcceptValidMtu(string value)
        {
            var configuration = new ServerConfiguration();
            configuration.MtuProperty.Value = value;

            configuration.MtuProperty.Validation.Validate(configuration.MtuProperty)
                .Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1279")]
        [InlineData("65536")]
        [InlineData("not-a-number")]
        public void ShouldRejectInvalidMtu(string value)
        {
            var configuration = new ServerConfiguration();
            configuration.MtuProperty.Value = value;

            configuration.MtuProperty.Validation.Validate(configuration.MtuProperty)
                .Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ShouldUseMtuForServerAndGeneratedClientConfigurations()
        {
            var configuration = new ServerConfiguration();

            configuration.MtuProperty.TargetTypes.Should()
                .Contain(typeof(ServerConfiguration))
                .And.Contain(typeof(ClientConfiguration));
        }

        [Fact]
        public void ShouldWriteMtu1500ToGeneratedServerAndClientConfigurations()
        {
            var configuration = new ServerConfiguration();
            configuration.MtuProperty.Value = "1500";

            var serverConfiguration = configuration.ToConfiguration<ServerConfiguration>();
            var clientConfiguration = configuration.ToConfiguration<ClientConfiguration>();

            serverConfiguration["Interface"]["MTU"].StringValue.Should().Be("1500");
            clientConfiguration["Peer"]["MTU"].StringValue.Should().Be("1500");
        }

        [Fact]
        public void ShouldUseTheFirstHostAddressForTheDefaultServerNetwork()
        {
            var configuration = new ServerConfiguration();

            configuration.AddressProperty.Validation.Validate(configuration.AddressProperty)
                .Should().BeNullOrEmpty();
            configuration.IpAddress.Should().Be("10.253.0.1");
        }
    }
}
