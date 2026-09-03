using System;
using System.IO;
using System.Text;

namespace WireGuardServerForWindows.Models
{
    public static class ServerConfigurationBackupWriter
    {
        public static string SaveToDesktop(ServerConfiguration configuration)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Save(configuration, desktop);
        }

        public static string Save(ServerConfiguration configuration, string directory)
        {
            Directory.CreateDirectory(directory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string serverName = SanitizeFileName(configuration.NameProperty.Value);
            string fileName = $"Wireguard Server Backup - {serverName} - {timestamp}.txt";
            string path = Path.Combine(directory, fileName);

            File.WriteAllText(path, CreateBackupText(configuration), Encoding.UTF8);
            return path;
        }

        public static string CreateBackupText(ServerConfiguration configuration)
        {
            var text = new StringBuilder();
            text.AppendLine("Wireguard Server Backup");
            text.AppendLine("=======================");
            text.AppendLine();
            text.AppendLine("IMPORTANT");
            text.AppendLine("This file contains VPN secrets, including private and preshared keys.");
            text.AppendLine("Keep it somewhere safe. Anyone with these keys may be able to recreate or impersonate this VPN configuration.");
            text.AppendLine();
            text.AppendLine($"Created: {DateTime.Now:G}");
            text.AppendLine();
            text.AppendLine("Server identity");
            text.AppendLine($"Name: {configuration.NameProperty.Value}");
            text.AppendLine($"Private key: {configuration.PrivateKeyProperty.Value}");
            text.AppendLine("  Secret. Generate a new one only for a new server, or keep this one when restoring this exact server.");
            text.AppendLine($"Public key: {configuration.PublicKeyProperty.Value}");
            text.AppendLine("  Safe to share with clients. It is created from the private key.");
            text.AppendLine($"Preshared key: {configuration.PresharedKeyProperty.Value}");
            text.AppendLine("  Secret. Clients must use the same preshared key if this option is enabled.");
            text.AppendLine();
            text.AppendLine("Network settings");
            text.AppendLine($"Listen port: {configuration.ListenPortProperty.Value}");
            text.AppendLine("  Forward this UDP port from your router/firewall to this Windows server.");
            text.AppendLine($"Endpoint: {configuration.EndpointProperty.Value}");
            text.AppendLine("  This is the public address and port clients use to connect.");
            text.AppendLine($"VPN network: {configuration.AddressProperty.Value}");
            text.AppendLine("  Private VPN-only network. Keep it different from the normal LAN.");
            text.AppendLine($"Allowed IPs: {configuration.AllowedIpsProperty.Value}");
            text.AppendLine("  0.0.0.0/0 sends all IPv4 client internet traffic through this server.");
            text.AppendLine($"MTU: {configuration.MtuProperty.Value}");
            text.AppendLine("  1420 is safest for WireGuard. 1500 should be tested carefully.");
            text.AppendLine();
            text.AppendLine("Safety settings");
            text.AppendLine($"Kill switch: {configuration.KillSwitchProperty.Value}");
            text.AppendLine($"DNS leak protection: {configuration.DnsLeakProtectionProperty.Value}");
            text.AppendLine($"Disable IPv6: {configuration.DisableIpv6Property.Value}");
            text.AppendLine($"Persistent keepalive: {configuration.PersistentKeepaliveProperty.Value}");
            text.AppendLine();
            text.AppendLine("Restore note");
            text.AppendLine("Use these values only when restoring this server or intentionally migrating the same VPN identity to another PC.");

            return text.ToString();
        }

        private static string SanitizeFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Server" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name;
        }
    }
}
