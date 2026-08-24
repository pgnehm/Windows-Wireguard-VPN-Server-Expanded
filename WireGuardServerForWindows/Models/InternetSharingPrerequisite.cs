using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using SharpConfig;
using WireGuardServerForWindows.Controls;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    /// <summary>
    /// Configures Windows NAT (WinNAT) for the WireGuard network.
    ///
    /// This replaces Internet Connection Sharing. WinNAT is a persistent
    /// Windows networking object and does not depend on ICS's fragile adapter
    /// sharing state or the SharedAccess reboot workaround.
    /// </summary>
    public class InternetSharingPrerequisite : PrerequisiteItem
    {
        public InternetSharingPrerequisite() : base
        (
            title: Resources.InternetSharingTitle,
            successMessage: Resources.InternetSharingSuccess,
            errorMessage: Resources.InternetSharingError,
            resolveText: Resources.InternetSharingResolve,
            configureText: Resources.InternetSharingConfigure
        )
        {
        }

        public override BooleanTimeCachedProperty Fulfilled => _fulfilled ??= new BooleanTimeCachedProperty(TimeSpan.FromSeconds(1), () =>
        {
            string networkPrefix = GetConfiguredNetworkPrefix();
            return !string.IsNullOrEmpty(networkPrefix)
                && WindowsNatManager.IsConfigured(ServerConfigurationPrerequisite.WireGuardServerInterfaceName, networkPrefix, out _);
        });
        private BooleanTimeCachedProperty _fulfilled;

        public override void Resolve()
        {
            Resolve(default);
        }

        /// <summary>
        /// The network parameter is retained for CLI compatibility. WinNAT is
        /// not bound to one public adapter; Windows chooses the normal route.
        /// </summary>
        public void Resolve(string networkToShare)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            WarningMessage = null;
            ErrorMessage = null;

            string networkPrefix = GetConfiguredNetworkPrefix();
            if (string.IsNullOrEmpty(networkPrefix))
            {
                ErrorMessage = Resources.InternetSharingError;
            }
            else if (WindowsNatManager.TryConfigure(
                ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                networkPrefix,
                out string error) == false)
            {
                ErrorMessage = string.IsNullOrEmpty(error)
                    ? Resources.InternetSharingError
                    : $"{Resources.InternetSharingError} {error}";
            }
            else
            {
                NetworkPathStatus path = NetworkDiagnostics.CheckInternetPath();
                bool natConfigured = WindowsNatManager.IsConfigured(
                    ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                    networkPrefix,
                    out string natError);
                NetworkPreflightResult preflight = NetworkPreflight.Evaluate(networkPrefix, path, natConfigured);
                if (!preflight.IsHealthy)
                {
                    WarningMessage = preflight.Summary;
                    if (!preflight.CanConfigure && !string.IsNullOrEmpty(natError))
                    {
                        WarningMessage = $"{WarningMessage} {natError}";
                    }
                }
            }

            Refresh();
            Mouse.OverrideCursor = null;
        }

        public override void Configure()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (!WindowsNatManager.TryRemove(
                    ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                    out string error))
            {
                ErrorMessage = string.IsNullOrEmpty(error) ? Resources.InternetSharingError : error;
            }
            WarningMessage = null;
            Refresh();
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Re-applies a previously created NAT after boot if Windows has not yet
        /// restored forwarding on the WireGuard interface.
        /// </summary>
        public bool TryRecover(out string error)
        {
            error = null;
            string networkPrefix = GetConfiguredNetworkPrefix();
            if (string.IsNullOrEmpty(networkPrefix) || !WindowsNatManager.Exists(out error))
            {
                return false;
            }

            if (WindowsNatManager.IsConfigured(ServerConfigurationPrerequisite.WireGuardServerInterfaceName, networkPrefix, out _))
            {
                return true;
            }

            return WindowsNatManager.TryConfigure(
                ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                networkPrefix,
                out error);
        }

        /// <summary>
        /// Returns a compatibility marker for the legacy CLI. There is no
        /// single public adapter associated with a WinNAT object.
        /// </summary>
        public List<string> GetSharedNetworks()
        {
            string networkPrefix = GetConfiguredNetworkPrefix();
            return !string.IsNullOrEmpty(networkPrefix)
                && WindowsNatManager.IsConfigured(ServerConfigurationPrerequisite.WireGuardServerInterfaceName, networkPrefix, out _)
                ? new List<string> { WindowsNatManager.NatName }
                : new List<string>();
        }

        private static string GetConfiguredNetworkPrefix()
        {
            if (File.Exists(ServerConfigurationPrerequisite.ServerDataPath) == false)
            {
                return null;
            }

            try
            {
                var configuration = new ServerConfiguration()
                    .Load<ServerConfiguration>(Configuration.LoadFromFile(ServerConfigurationPrerequisite.ServerDataPath));
                return configuration.AddressProperty.Value;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class WindowsNatManager
    {
        public const string NatName = "WS4W-WireGuard";

        public static bool Exists(out string error)
        {
            string script = $@"
$ErrorActionPreference = 'Stop'
if ($null -eq (Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue)) {{ exit 10 }}
exit 0
";

            CommandResult result = RunPowerShell(script);
            error = result.Error;
            return result.ExitCode == 0;
        }

        public static bool IsConfigured(string interfaceAlias, string networkPrefix, out string error)
        {
            error = null;
            if (TryNormalizeIPv4Prefix(networkPrefix, out string normalizedPrefix) == false)
            {
                return false;
            }

            string script = $@"
$ErrorActionPreference = 'Stop'
$nat = Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue
$interface = Get-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -ErrorAction SilentlyContinue
if ($null -eq $nat -or $null -eq $interface) {{ exit 10 }}
if ([string]$nat.InternalIPInterfaceAddressPrefix -ne {Quote(normalizedPrefix)}) {{ exit 11 }}
if ([string]$interface.Forwarding -ne 'Enabled') {{ exit 12 }}
exit 0
";

            CommandResult result = RunPowerShell(script);
            if (result.ExitCode == 0)
            {
                return true;
            }

            error = result.Error;
            return false;
        }

        public static bool TryConfigure(string interfaceAlias, string networkPrefix, out string error)
        {
            error = null;
            if (TryNormalizeIPv4Prefix(networkPrefix, out string normalizedPrefix) == false)
            {
                error = "The WireGuard network must be a valid IPv4 CIDR network.";
                return false;
            }

            string script = $@"
$ErrorActionPreference = 'Stop'
$oldNat = $null
$oldNatPrefix = $null
$interface = $null
$oldForwarding = $null
try {{
    $oldNat = Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue
    if ($null -ne $oldNat) {{ $oldNatPrefix = [string]$oldNat.InternalIPInterfaceAddressPrefix }}
    $interface = Get-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -ErrorAction SilentlyContinue
    if ($null -eq $interface) {{ throw 'The WireGuard network interface was not found.' }}
    $oldForwarding = [string]$interface.Forwarding

    if ($null -ne $oldNat -and $oldNatPrefix -ne {Quote(normalizedPrefix)}) {{
        Remove-NetNat -Name {Quote(NatName)} -Confirm:$false
        $oldNat = $null
    }}
    Set-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -Forwarding Enabled
    if ($null -eq $oldNat) {{
        New-NetNat -Name {Quote(NatName)} -InternalIPInterfaceAddressPrefix {Quote(normalizedPrefix)} | Out-Null
    }}
}}
catch {{
    try {{
        $currentNat = Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue
        if ($null -ne $currentNat -and $null -eq $oldNatPrefix) {{ Remove-NetNat -Name {Quote(NatName)} -Confirm:$false }}
        if ($null -ne $oldNatPrefix) {{
            if ($null -ne $currentNat) {{ Remove-NetNat -Name {Quote(NatName)} -Confirm:$false }}
            New-NetNat -Name {Quote(NatName)} -InternalIPInterfaceAddressPrefix $oldNatPrefix | Out-Null
        }}
        if ($null -ne $interface -and $oldForwarding -ne 'Enabled') {{
            Set-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -Forwarding Disabled
        }}
    }} catch {{ }}
    throw
}}
";

            CommandResult result = RunPowerShell(script);
            if (result.ExitCode == 0)
            {
                return true;
            }

            error = result.Error;
            return false;
        }

        public static bool TryRemove(string interfaceAlias, out string error)
        {
            string script = $@"
$ErrorActionPreference = 'Stop'
$nat = Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue
$interface = Get-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -ErrorAction SilentlyContinue
$oldForwarding = if ($null -ne $interface) {{ [string]$interface.Forwarding }} else {{ $null }}
try {{
    if ($null -ne $nat) {{ Remove-NetNat -Name {Quote(NatName)} -Confirm:$false }}
    if ($null -ne $interface -and $oldForwarding -ne 'Enabled') {{ Set-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -Forwarding Disabled }}
}}
catch {{
    try {{
        if ($null -ne $nat -and $null -eq (Get-NetNat -Name {Quote(NatName)} -ErrorAction SilentlyContinue)) {{
            New-NetNat -Name {Quote(NatName)} -InternalIPInterfaceAddressPrefix ([string]$nat.InternalIPInterfaceAddressPrefix) | Out-Null
        }}
        if ($null -ne $interface -and $oldForwarding -eq 'Enabled') {{ Set-NetIPInterface -InterfaceAlias {Quote(interfaceAlias)} -AddressFamily IPv4 -Forwarding Enabled }}
    }} catch {{ }}
    throw
}}
";

            CommandResult result = RunPowerShell(script);
            error = result.Error;
            return result.ExitCode == 0;
        }

        private static bool TryNormalizeIPv4Prefix(string networkPrefix, out string normalizedPrefix)
        {
            normalizedPrefix = null;
            if (string.IsNullOrWhiteSpace(networkPrefix)
                || networkPrefix.Contains('/') == false
                || !System.Net.IPNetwork.TryParse(networkPrefix, out var network)
                || network.BaseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            normalizedPrefix = $"{network.BaseAddress}/{network.PrefixLength}";
            return true;
        }

        private static string Quote(string value)
        {
            return $"'{value.Replace("'", "''")}'";
        }

        private static CommandResult RunPowerShell(string script)
        {
            string windowsDirectory = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
            string executable = Path.Combine(windowsDirectory, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
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
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (process.WaitForExit(30_000) == false)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return new CommandResult(-1, "The Windows PowerShell command timed out.");
                }

                Task.WaitAll(outputTask, errorTask);
                string error = errorTask.Result.Trim();
                return new CommandResult(process.ExitCode, string.IsNullOrEmpty(error) ? outputTask.Result.Trim() : error);
            }
            catch (Exception exception)
            {
                return new CommandResult(-1, exception.Message);
            }
        }

        private readonly struct CommandResult
        {
            public CommandResult(int exitCode, string error)
            {
                ExitCode = exitCode;
                Error = error;
            }

            public int ExitCode { get; }
            public string Error { get; }
        }
    }
}
