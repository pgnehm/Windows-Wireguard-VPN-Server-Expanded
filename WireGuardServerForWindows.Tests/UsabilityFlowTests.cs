using FluentAssertions;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class UsabilityFlowTests
    {
        [Fact]
        public void ServerConfigurationShouldSeparateBasicAndAdvancedFields()
        {
            var configuration = new ServerConfiguration();

            configuration.BasicUiProperties.Should().Contain(configuration.NameProperty);
            configuration.BasicUiProperties.Should().Contain(configuration.ListenPortProperty);
            configuration.BasicUiProperties.Should().Contain(configuration.EndpointProperty);
            configuration.AdvancedUiProperties.Should().Contain(configuration.AllowedIpsProperty);
            configuration.AdvancedUiProperties.Should().Contain(configuration.MtuProperty);
            configuration.AdvancedUiProperties.Should().Contain(configuration.PrivateKeyProperty);
            configuration.HasAdvancedUiProperties.Should().BeTrue();
        }

        [Fact]
        public void ClientListShouldStartEmptyAndSelectNewClientAfterAdd()
        {
            var clients = new ClientConfigurationList();

            clients.HasClients.Should().BeFalse();
            clients.SelectedClient.Should().BeNull();

            ClientConfiguration client = clients.AddClientWithDefaults();

            clients.HasClients.Should().BeTrue();
            clients.SelectedClient.Should().BeSameAs(client);
            client.NameProperty.Value.Should().Be("Client 1");
        }

        [Fact]
        public void ClientConfigurationShouldKeepOnlyDeviceNameInBasicFields()
        {
            var client = new ClientConfiguration(new ClientConfigurationList());

            client.ShowTopLevelActions.Should().BeFalse();
            client.BasicUiProperties.Should().ContainSingle().Which.Should().Be(client.NameProperty);
            client.AdvancedUiProperties.Should().Contain(client.AddressProperty);
            client.AdvancedUiProperties.Should().Contain(client.DnsProperty);
            client.AdvancedUiProperties.Should().Contain(client.PrivateKeyProperty);
        }

        [Fact]
        public void ClientDnsShouldDefaultToGoogleAndExposeCommonChoices()
        {
            var client = new ClientConfiguration(new ClientConfigurationList());

            client.DnsProperty.Value.Should().Be("8.8.8.8, 8.8.4.4");
            client.DnsProperty.Options.Should().Contain("8.8.8.8, 8.8.4.4");
            client.DnsProperty.Options.Should().Contain("1.1.1.1, 1.0.0.1");
            client.DnsProperty.HasOptions.Should().BeTrue();
        }
    }
}
