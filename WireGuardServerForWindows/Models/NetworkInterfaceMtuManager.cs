using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace WireGuardServerForWindows.Models
{
    /// <summary>
    /// Applies the configured MTU to the live WireGuard adapter. The MTU is also written
    /// to the WireGuard configuration, but changing an existing tunnel requires updating
    /// the Windows interface itself.
    /// </summary>
    public static class NetworkInterfaceMtuManager
    {
        public static bool TryApply(string interfaceName, string mtuValue, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                error = "The network interface name is empty.";
                return false;
            }

            if (!int.TryParse(mtuValue, out int mtu) || mtu < 1280 || mtu > 65535)
            {
                error = "The MTU must be between 1280 and 65535.";
                return false;
            }

            if (!NetworkInterface.GetAllNetworkInterfaces().Any(n =>
                    string.Equals(n.Name, interfaceName, StringComparison.OrdinalIgnoreCase)))
            {
                // The tunnel may not exist yet. WireGuard will apply MTU when it creates
                // the adapter from the configuration file.
                return true;
            }

            if (!TrySetFamily("ipv4", interfaceName, mtu, out error))
            {
                return false;
            }

            // IPv6 may not be installed on the host. Try it for parity, but do not make
            // an otherwise working IPv4 tunnel fail on hosts without IPv6 support.
            TrySetFamily("ipv6", interfaceName, mtu, out _);
            return true;
        }

        private static bool TrySetFamily(string addressFamily, string interfaceName, int mtu, out string error)
        {
            error = null;

            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            })
            {
                process.StartInfo.ArgumentList.Add("interface");
                process.StartInfo.ArgumentList.Add(addressFamily);
                process.StartInfo.ArgumentList.Add("set");
                process.StartInfo.ArgumentList.Add("subinterface");
                process.StartInfo.ArgumentList.Add($"name={interfaceName}");
                process.StartInfo.ArgumentList.Add($"mtu={mtu}");
                process.StartInfo.ArgumentList.Add("store=persistent");

                try
                {
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    error = $"Unable to apply the MTU with netsh: {ex.Message}";
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    string details = process.StandardError.ReadToEnd().Trim();
                    error = string.IsNullOrEmpty(details)
                        ? $"netsh could not set the {addressFamily} MTU."
                        : details;
                    return false;
                }
            }

            return true;
        }
    }
}
