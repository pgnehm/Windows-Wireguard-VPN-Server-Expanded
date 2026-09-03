using System;
using System.IO;
using FluentAssertions;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class ServerConfigurationUsabilityTests
    {
        [Fact]
        public void ServerNameShouldDefaultToMachineName()
        {
            var configuration = new ServerConfiguration();

            configuration.NameProperty.Value.Should().Be($"{Environment.MachineName} Wireguard Server");
        }

        [Fact]
        public void BooleanServerSettingsShouldExposeDropdownOptions()
        {
            var configuration = new ServerConfiguration();

            configuration.KillSwitchProperty.Options.Should().Equal(bool.TrueString, bool.FalseString);
            configuration.DnsLeakProtectionProperty.Options.Should().Equal(bool.TrueString, bool.FalseString);
            configuration.DisableIpv6Property.Options.Should().Equal(bool.TrueString, bool.FalseString);
        }

        [Fact]
        public void VisibleServerSettingsShouldHavePlainLanguageDescriptions()
        {
            var configuration = new ServerConfiguration();

            configuration.UiProperties.Should().OnlyContain(property => string.IsNullOrWhiteSpace(property.Description) == false);
        }

        [Fact]
        public void BackupTextShouldExplainSensitiveServerSettings()
        {
            ServerConfiguration configuration = CreateCompleteConfiguration();

            string text = ServerConfigurationBackupWriter.CreateBackupText(configuration);

            text.Should().Contain("This file contains VPN secrets");
            text.Should().Contain("Private key: server-private");
            text.Should().Contain("Public key: server-public");
            text.Should().Contain("Preshared key: server-preshared");
            text.Should().Contain("Forward this UDP port");
            text.Should().Contain("Restore note");
        }

        [Fact]
        public void BackupFileShouldUseFriendlyServerName()
        {
            ServerConfiguration configuration = CreateCompleteConfiguration();
            string directory = Path.Combine(Path.GetTempPath(), "WireguardServerTests", Guid.NewGuid().ToString("N"));

            try
            {
                string path = ServerConfigurationBackupWriter.Save(configuration, directory);

                File.Exists(path).Should().BeTrue();
                Path.GetFileName(path).Should().StartWith("Wireguard Server Backup - Kitchen Server - ");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        private static ServerConfiguration CreateCompleteConfiguration()
        {
            var configuration = new ServerConfiguration();
            configuration.NameProperty.Value = "Kitchen Server";
            configuration.PrivateKeyProperty.Value = "server-private";
            configuration.PublicKeyProperty.Value = "server-public";
            configuration.PresharedKeyProperty.Value = "server-preshared";
            configuration.ListenPortProperty.Value = "51820";
            configuration.EndpointProperty.Value = "vpn.example.com:51820";
            configuration.AddressProperty.Value = "10.253.0.0/24";
            configuration.AllowedIpsProperty.Value = "0.0.0.0/0";
            configuration.MtuProperty.Value = "1420";
            configuration.KillSwitchProperty.Value = bool.FalseString;
            configuration.DnsLeakProtectionProperty.Value = bool.TrueString;
            configuration.DisableIpv6Property.Value = bool.TrueString;
            configuration.PersistentKeepaliveProperty.Value = "0";
            return configuration;
        }
    }
}
