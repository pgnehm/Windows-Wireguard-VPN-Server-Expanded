using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WireGuardServerForWindows.Models
{
    /// <summary>
    /// Applies narrowly scoped firewall rules for the configured WireGuard subnet.
    /// It never creates a general-purpose forwarding or proxy rule.
    /// </summary>
    internal static class NetworkSecurityManager
    {
        private const string ListenRule = "WS4W-WireGuard-Listen";
        private const string InterfaceRule = "WS4W-WireGuard-Interface";
        private const string KillSwitchRule = "WS4W-WireGuard-KillSwitch";
        private const string Ipv6Rule = "WS4W-WireGuard-IPv6Disabled";

        public static bool TryApply(ServerConfiguration configuration, string interfaceAlias, out string error)
        {
            error = null;
            if (!System.Net.IPNetwork.TryParse(configuration.AddressProperty.Value, out var network)
                || network.BaseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                error = "Firewall rules require an IPv4 WireGuard subnet.";
                return false;
            }

            if (!int.TryParse(configuration.ListenPortProperty.Value, out int listenPort))
            {
                error = "The WireGuard listen port is invalid.";
                return false;
            }

            string prefix = $"{network.BaseAddress}/{network.PrefixLength}";
            string script = $@"
$ErrorActionPreference = 'Stop'
$names = @({Quote(ListenRule)}, {Quote(InterfaceRule)}, {Quote(KillSwitchRule)}, {Quote(Ipv6Rule)})
foreach ($name in $names) {{ Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue }}
New-NetFirewallRule -DisplayName {Quote(ListenRule)} -Direction Inbound -Action Allow -Protocol UDP -LocalPort {listenPort} -Profile Any | Out-Null
New-NetFirewallRule -DisplayName {Quote(InterfaceRule)} -Direction Inbound -Action Allow -InterfaceAlias {Quote(interfaceAlias)} -LocalAddress {Quote(prefix)} -Profile Any | Out-Null
";

            if (bool.TryParse(configuration.KillSwitchProperty.Value, out bool killSwitch) && killSwitch)
            {
                script += $@"
New-NetFirewallRule -DisplayName {Quote(KillSwitchRule)} -Direction Outbound -Action Block -LocalAddress {Quote(prefix)} -Profile Any | Out-Null
";
            }

            if (bool.TryParse(configuration.DisableIpv6Property.Value, out bool disableIpv6) && disableIpv6)
            {
                script += $@"
New-NetFirewallRule -DisplayName {Quote(Ipv6Rule)} -Direction Outbound -Action Block -InterfaceAlias {Quote(interfaceAlias)} -RemoteAddress '::/0' -Profile Any | Out-Null
";
            }

            CommandResult result = RunPowerShell(script);
            error = result.Error;
            return result.ExitCode == 0;
        }

        public static bool TryRemove(out string error)
        {
            string script = $@"
$ErrorActionPreference = 'Stop'
$names = @({Quote(ListenRule)}, {Quote(InterfaceRule)}, {Quote(KillSwitchRule)}, {Quote(Ipv6Rule)})
foreach ($name in $names) {{ Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue }}
";
            CommandResult result = RunPowerShell(script);
            error = result.Error;
            return result.ExitCode == 0;
        }

        private static string Quote(string value) => $"'{value.Replace("'", "''")}'";

        private static CommandResult RunPowerShell(string script)
        {
            string windowsDirectory = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
            string executable = Path.Combine(windowsDirectory, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-EncodedCommand");
            process.StartInfo.ArgumentList.Add(encodedCommand);
            try
            {
                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30_000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return new CommandResult(-1, "The Windows PowerShell command timed out.");
                }
                Task.WaitAll(output, error);
                return new CommandResult(process.ExitCode, string.IsNullOrWhiteSpace(error.Result) ? output.Result.Trim() : error.Result.Trim());
            }
            catch (Exception exception)
            {
                return new CommandResult(-1, exception.Message);
            }
        }

        private readonly struct CommandResult
        {
            public CommandResult(int exitCode, string error) { ExitCode = exitCode; Error = error; }
            public int ExitCode { get; }
            public string Error { get; }
        }
    }
}
