using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Net;
using SharpConfig;
using WireGuardAPI;
using WireGuardAPI.Commands;
using WireGuardServerForWindows.Controls;
using WireGuardServerForWindows.Extensions;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    public class ServerConfigurationPrerequisite : PrerequisiteItem
    {
        #region Constructor

        public ServerConfigurationPrerequisite() : base
        (
            title: Resources.ServerConfiguration,
            successMessage: Resources.ServerConfigurationSuccessMessage,
            errorMessage: Resources.ServerConfigurationMissingErrorMessage,
            resolveText: Resources.ServerConfigurationConfigureText,
            configureText: Resources.ServerConfigurationConfigureText
        ) { }

        #endregion

        #region PrerequisiteItem members

        public override BooleanTimeCachedProperty Fulfilled => _fulfilled ??= new BooleanTimeCachedProperty(TimeSpan.FromSeconds(1), () =>
        {
            if (File.Exists(ServerWGPath) == false)
            {
                ErrorMessage = Resources.ServerConfigurationMissingErrorMessage;
                return false;
            }
            
            
            // The file exists, make sure it has all the fields
            var serverConfiguration = new ServerConfiguration().Load<ServerConfiguration>(Configuration.LoadFromFile(ServerDataPath));

            foreach (ConfigurationProperty property in serverConfiguration.Properties)
            {
                if (string.IsNullOrEmpty(property.Validation?.Validate?.Invoke(property)) == false)
                {
                    ErrorMessage = Resources.ServerConfigurationIncompleteErrorMessage;
                    return false;
                }
            }

            // If we get here, everything passed.
            return true;
        });
        private BooleanTimeCachedProperty _fulfilled;

        public override void Resolve()
        {
            if (Directory.Exists(Path.GetDirectoryName(ServerDataPath)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ServerDataPath));
            }

            if (File.Exists(ServerDataPath) == false)
            {
#pragma warning disable CS0642
                // There is intentionally no code block after the using statement,
                // because we want to create and then release the file without holding it open.
                using (File.Create(ServerDataPath));
#pragma warning restore CS0642
            }

            if (Directory.Exists(Path.GetDirectoryName(ServerWGPath)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ServerWGPath));
            }

            if (File.Exists(ServerWGPath) == false)
            {
#pragma warning disable CS0642
                // There is intentionally no code block after the using statement,
                // because we want to create and then release the file without holding it open.
                using (File.Create(ServerWGPath));
#pragma warning restore CS0642
            }

            Configure();
        }

        public override void Configure()
        {
            var serverConfiguration = new ServerConfiguration().Load<ServerConfiguration>(Configuration.LoadFromFile(ServerDataPath));
            ServerConfigurationEditorWindow serverConfigurationEditor = new ServerConfigurationEditorWindow {DataContext = serverConfiguration};

            Mouse.OverrideCursor = Cursors.Wait;
            if (serverConfigurationEditor.ShowDialog() == true)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                WarningMessage = null;

                // Save to Data
                SaveData(serverConfiguration);

                // Save to WG
                SaveWG(serverConfiguration);

                // Update clients
                var clientConfigurationsPrerequisite = new ClientConfigurationsPrerequisite();
                clientConfigurationsPrerequisite.Update();

                // Update WinNAT if the WireGuard network range changed.
                if (string.IsNullOrEmpty(serverConfiguration.AddressProperty.Validation?.Validate?.Invoke(serverConfiguration.AddressProperty)))
                {
                    var nat = new InternetSharingPrerequisite();
                    if (WindowsNatManager.Exists(out _))
                    {
                        nat.Configure();
                        nat.Resolve();
                    }
                }

                // Update the tunnel service, if everyone is happy
                if (Fulfilled && clientConfigurationsPrerequisite.Fulfilled && new TunnelServicePrerequisite().Fulfilled)
                {
                    // Sync conf to tunnel
                    new WireGuardExe().ExecuteCommand(new SyncConfigurationCommand(WireGuardServerInterfaceName, ServerWGPath));
                    ApplyConfiguredMtu(serverConfiguration);
                    ApplyNetworkSecurity(serverConfiguration);
                }

                Mouse.OverrideCursor = null;
            }

            Refresh();
        }

        public override void Update()
        {
            if (File.Exists(ServerDataPath))
            {
                WarningMessage = null;
                var serverConfiguration = new ServerConfiguration().Load<ServerConfiguration>(Configuration.LoadFromFile(ServerDataPath));
                SaveWG(serverConfiguration);
                ApplyConfiguredMtu(serverConfiguration);
                ApplyNetworkSecurity(serverConfiguration);
            }

            Refresh();
        }

        #endregion

        #region Private methods

        private void ApplyConfiguredMtu(ServerConfiguration serverConfiguration)
        {
            if (NetworkInterfaceMtuManager.TryApply(WireGuardServerInterfaceName, serverConfiguration.MtuProperty.Value, out var error))
            {
                return;
            }

            WarningMessage = $"The configured MTU could not be applied to the running tunnel: {error}";
        }

        private void ApplyNetworkSecurity(ServerConfiguration serverConfiguration)
        {
            if (NetworkSecurityManager.TryApply(serverConfiguration, WireGuardServerInterfaceName, out var error))
            {
                return;
            }

            WarningMessage = string.IsNullOrEmpty(WarningMessage)
                ? $"Firewall protection could not be applied: {error}"
                : $"{WarningMessage} Firewall protection could not be applied: {error}";
        }

        private void SaveData(ServerConfiguration serverConfiguration)
        {
            serverConfiguration.ToConfiguration().SaveToFile(ServerDataPath);
        }

        private void SaveWG(ServerConfiguration serverConfiguration)
        {
            var configuration = serverConfiguration.ToConfiguration<ServerConfiguration>();

            if (Directory.Exists(ClientConfigurationsPrerequisite.ClientDataDirectory))
            {
                foreach (string clientConfigurationFile in Directory.GetFiles(ClientConfigurationsPrerequisite.ClientDataDirectory, "*.conf"))
                {
                    var clientConfiguration = new ClientConfiguration(null).Load<ClientConfiguration>(Configuration.LoadFromFile(clientConfigurationFile));
                    clientConfiguration.ServerPersistentKeepaliveProperty.Value = serverConfiguration.PersistentKeepaliveProperty.Value;
                    
                    configuration = configuration.Merge(clientConfiguration.ToConfiguration<ServerConfiguration>());
                }
            }

            configuration.SaveToFile(ServerWGPath);
        }

        #endregion

        #region Public static properties

        public static string ServerWGPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WS4W", "server_wg", "wg_server.conf");

        public static string ServerDataPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WS4W", "server_data", "wg_server.conf");

        public static string WireGuardServerInterfaceName => Path.GetFileNameWithoutExtension(ServerWGPath);

        #endregion

        #region Public static methods

        public static Network GetNetwork(TimeSpan? timeout = null)
        {
            Network result = default;

            Stopwatch stopwatch = Stopwatch.StartNew();

            do
            {
                // Windows API code pack can show stale adapters, and incorrect names.
                // First, get the real interface here.
                if (NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(i => i.Name == WireGuardServerInterfaceName) is { } networkInterface)
                {
                    // Now use the ID to get the network from API code pack
                    if (NetworkListManager.GetNetworks(NetworkConnectivityLevels.All).FirstOrDefault(n => n.Connections.Any(c => c.AdapterId == new Guid(networkInterface.Id))) is { } network)
                    {
                        result = network;
                        break;
                    }
                }
            } while (stopwatch.ElapsedMilliseconds < (timeout?.TotalMilliseconds ?? 0));

            stopwatch.Stop();

            return result;
        }

        #endregion

    }
}
