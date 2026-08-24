using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Input;
using WireGuardAPI;
using WireGuardAPI.Commands;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    public class TunnelServicePrerequisite : PrerequisiteItem
    {
        public TunnelServicePrerequisite() : base
        (
            title: Resources.TunnelService,
            successMessage: Resources.TunnelServiceInstalled,
            errorMessage: Resources.TunnelServiceNotInstalled,
            resolveText: Resources.InstallTunnelService,
            configureText: Resources.UninstallTunnelService
        )
        {
        }

        public override BooleanTimeCachedProperty Fulfilled => _fulfilled ??= new BooleanTimeCachedProperty(TimeSpan.FromSeconds(1), () =>
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Any(nic => nic.Name == ServerConfigurationPrerequisite.WireGuardServerInterfaceName);
        });
        private BooleanTimeCachedProperty _fulfilled;

        public override async void Resolve()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            
            new WireGuardExe().ExecuteCommand(new InstallTunnelServiceCommand(ServerConfigurationPrerequisite.ServerWGPath));
            await WaitForFulfilled();

            if (File.Exists(ServerConfigurationPrerequisite.ServerDataPath))
            {
                var serverConfiguration = new ServerConfiguration()
                    .Load<ServerConfiguration>(SharpConfig.Configuration.LoadFromFile(ServerConfigurationPrerequisite.ServerDataPath));
                ApplyConfiguredMtu(serverConfiguration);
                if (!NetworkSecurityManager.TryApply(
                        serverConfiguration,
                        ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                        out string securityError))
                {
                    WarningMessage = string.IsNullOrEmpty(WarningMessage)
                        ? $"Firewall protection could not be applied: {securityError}"
                        : $"{WarningMessage} Firewall protection could not be applied: {securityError}";
                }
            }

            Mouse.OverrideCursor = null;
        }

        public override async void Configure()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            
            new WireGuardExe().ExecuteCommand(new UninstallTunnelServiceCommand(ServerConfigurationPrerequisite.WireGuardServerInterfaceName));
            await WaitForFulfilled(false);
            string securityError;
            NetworkSecurityManager.TryRemove(out securityError);
            
            Mouse.OverrideCursor = null;
        }

        private void ApplyConfiguredMtu(ServerConfiguration serverConfiguration)
        {
            if (NetworkInterfaceMtuManager.TryApply(
                    ServerConfigurationPrerequisite.WireGuardServerInterfaceName,
                    serverConfiguration.MtuProperty.Value,
                    out var error))
            {
                WarningMessage = null;
                return;
            }

            WarningMessage = $"The tunnel is running, but the configured MTU could not be applied: {error}";
        }
    }
}
