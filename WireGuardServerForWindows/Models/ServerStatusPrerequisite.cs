using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using WireGuardAPI;
using WireGuardAPI.Commands;
using WireGuardServerForWindows.Controls;

namespace WireGuardServerForWindows.Models
{
    public class ServerStatusPrerequisite : PrerequisiteItem
    {
        public ServerStatusPrerequisite() : base
        (
            title: "View Server Status",
            successMessage: string.Empty,
            errorMessage: string.Empty,
            resolveText: string.Empty,
            configureText: "View"
        )
        {
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _updateTimer.Tick += (_, __) =>
            {
                if (UpdateLive)
                {
                    RefreshDiagnostics();
                }
            };

            RefreshDiagnostics();
        }

        public override BooleanTimeCachedProperty Fulfilled { get; } =
            new BooleanTimeCachedProperty(TimeSpan.FromSeconds(1), () => true);

        public override void Resolve() => throw new NotImplementedException();

        public override void Configure()
        {
            RefreshDiagnostics();
            _updateTimer.IsEnabled = true;
            new ServerStatusWindow { DataContext = this }.ShowDialog();
            _updateTimer.IsEnabled = false;
        }

        public override BooleanTimeCachedProperty IsInformational { get; } =
            new BooleanTimeCachedProperty(TimeSpan.Zero, () => true);

        public WireGuardStatusSnapshot WireGuard => _wireGuard;
        public NetworkPathStatus NetworkPath => _networkPath;
        public string ServerRunning => _wireGuard.IsRunning ? "Running" : "Stopped";
        public string LastClientHandshake => _wireGuard.LastClientHandshake;
        public string BytesReceived => _wireGuard.BytesReceived;
        public string BytesSent => _wireGuard.BytesSent;
        public string TransferStatus => $"{BytesReceived} / {BytesSent}";
        public string MtuCurrentlyApplied => GetCurrentMtu();
        public string InternetSharingStatus => GetInternetSharingStatus();
        public string DnsStatus => _networkPath.Dns;
        public string Ipv4Status => GetAddressFamilyStatus(System.Net.Sockets.AddressFamily.InterNetwork);
        public string Ipv6Status => GetAddressFamilyStatus(System.Net.Sockets.AddressFamily.InterNetworkV6);
        public string RoutingStatus => _networkPath.Routing;
        public string InternetAccessStatus => _networkPath.InternetAccess;
        public string UpstreamAdapter => _networkPath.Adapter;
        public string RecommendedFixes => GetRecommendedFixes();

        public RelayCommand RepairNatCommand => _repairNatCommand ??= new RelayCommand(() =>
        {
            new InternetSharingPrerequisite().Resolve();
            RefreshDiagnostics();
        });
        private RelayCommand _repairNatCommand;

        public RelayCommand ReapplyMtuCommand => _reapplyMtuCommand ??= new RelayCommand(() =>
        {
            new ServerConfigurationPrerequisite().Update();
            RefreshDiagnostics();
        });
        private RelayCommand _reapplyMtuCommand;

        public bool UpdateLive
        {
            get => _updateLive;
            set => Set(nameof(UpdateLive), ref _updateLive, value);
        }
        private bool _updateLive = true;

        private readonly DispatcherTimer _updateTimer;
        private WireGuardStatusSnapshot _wireGuard = new WireGuardStatusSnapshot();
        private NetworkPathStatus _networkPath = new NetworkPathStatus();

        private void RefreshDiagnostics()
        {
            try
            {
                string output = new WireGuardExe().ExecuteCommand(
                    new ShowCommand(ServerConfigurationPrerequisite.WireGuardServerInterfaceName));
                _wireGuard = WireGuardStatusParser.Parse(output);
            }
            catch (Exception exception)
            {
                _wireGuard = new WireGuardStatusSnapshot { IsRunning = false, Error = exception.Message };
            }

            _networkPath = NetworkDiagnostics.CheckInternetPath();

            RaisePropertyChanged(nameof(WireGuard));
            RaisePropertyChanged(nameof(NetworkPath));
            RaisePropertyChanged(nameof(ServerRunning));
            RaisePropertyChanged(nameof(LastClientHandshake));
            RaisePropertyChanged(nameof(BytesReceived));
            RaisePropertyChanged(nameof(BytesSent));
            RaisePropertyChanged(nameof(TransferStatus));
            RaisePropertyChanged(nameof(MtuCurrentlyApplied));
            RaisePropertyChanged(nameof(InternetSharingStatus));
            RaisePropertyChanged(nameof(DnsStatus));
            RaisePropertyChanged(nameof(Ipv4Status));
            RaisePropertyChanged(nameof(Ipv6Status));
            RaisePropertyChanged(nameof(RoutingStatus));
            RaisePropertyChanged(nameof(InternetAccessStatus));
            RaisePropertyChanged(nameof(UpstreamAdapter));
            RaisePropertyChanged(nameof(RecommendedFixes));
        }

        private string GetCurrentMtu()
        {
            NetworkInterface adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => string.Equals(i.Name, ServerConfigurationPrerequisite.WireGuardServerInterfaceName, StringComparison.OrdinalIgnoreCase));
            try
            {
                int mtu = adapter?.GetIPProperties().GetIPv4Properties()?.Mtu ?? 0;
                return mtu > 0 ? mtu.ToString() : "Not applied (interface not found)";
            }
            catch
            {
                return "Unable to read";
            }
        }

        private string GetInternetSharingStatus()
        {
            if (!File.Exists(ServerConfigurationPrerequisite.ServerDataPath)) return "Not configured";
            try
            {
                var configuration = new ServerConfiguration()
                    .Load<ServerConfiguration>(SharpConfig.Configuration.LoadFromFile(ServerConfigurationPrerequisite.ServerDataPath));
                return WindowsNatManager.IsConfigured(ServerConfigurationPrerequisite.WireGuardServerInterfaceName, configuration.AddressProperty.Value, out string error)
                    ? "Enabled"
                    : string.IsNullOrEmpty(error) ? "Disabled" : $"Disabled ({error})";
            }
            catch (Exception exception)
            {
                return $"Unavailable ({exception.Message})";
            }
        }

        private string GetAddressFamilyStatus(System.Net.Sockets.AddressFamily addressFamily)
        {
            NetworkInterface adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => string.Equals(i.Name, ServerConfigurationPrerequisite.WireGuardServerInterfaceName, StringComparison.OrdinalIgnoreCase));
            if (adapter == null) return "Disabled (interface not found)";
            if (adapter.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == addressFamily)) return "Enabled";
            return addressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? "Disabled (IPv4-only configuration)"
                : "Not applied";
        }

        private string GetRecommendedFixes()
        {
            var fixes = new List<string>();
            if (!_wireGuard.IsRunning) fixes.Add("Start or reinstall the WireGuard tunnel.");
            if (_wireGuard.PeerCount == 0) fixes.Add("Add a client configuration.");
            if (_wireGuard.PeerCount > 0 && _wireGuard.LastClientHandshake == "No handshake recorded")
                fixes.Add("Have a client connect and verify its endpoint and firewall port.");
            if (!_networkPath.HasConnectedAdapter) fixes.Add("Connect the server to an Ethernet or Wi-Fi upstream adapter.");
            if (!_networkPath.HasDns) fixes.Add("Configure a working DNS server on the upstream adapter.");
            if (!_networkPath.HasInternetAccess) fixes.Add("Verify the default route and upstream firewall allow HTTPS access.");
            if (GetInternetSharingStatus() != "Enabled") fixes.Add("Repair Windows NAT.");
            if (fixes.Count == 0) fixes.Add("No immediate fix is recommended.");
            return string.Join(Environment.NewLine, fixes.Select(f => $"• {f}"));
        }
    }
}
