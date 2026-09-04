using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SharpConfig;
using WireGuardServerForWindows.Controls;
using WireGuardServerForWindows.Models;

namespace WireGuardServerForWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Never put quotes around config file values
            Configuration.OutputRawStringValues = true;

            var wireGuardExePrerequisite = new WireGuardExePrerequisite();
            var serverConfigurationPrerequisite = new ServerConfigurationPrerequisite();
            var clientConfigurationsPrerequisite = new ClientConfigurationsPrerequisite();
            var tunnelServicePrerequisite = new TunnelServicePrerequisite();
            var privateNetworkPrerequisite = new PrivateNetworkPrerequisite();
            var internetSharingPrerequisite = new InternetSharingPrerequisite();
            var serverStatusPrerequisite = new ServerStatusPrerequisite();

            wireGuardExePrerequisite.HelpText =
                "WireGuard is the VPN engine. This app manages it, but WireGuard provides the secure encrypted tunnel that client devices connect through.";
            serverConfigurationPrerequisite.HelpText =
                "Set the server name and public address clients will use. The app fills in safe defaults, including port, VPN network, MTU, and keys.";
            clientConfigurationsPrerequisite.HelpText =
                "Create one profile per phone, laptop, or computer. Start with a device name; the app fills in address, DNS, and keys.";
            tunnelServicePrerequisite.HelpText =
                "The tunnel is the encrypted VPN connection. Installing the tunnel service tells Windows to run WireGuard in the background using the server and client settings you saved.";
            privateNetworkPrerequisite.HelpText =
                "Windows applies stricter firewall rules to Public networks. Marking the VPN network Private lets the server-side VPN/NAT traffic work without opening unrelated public access.";
            internetSharingPrerequisite.HelpText =
                "NAT means Network Address Translation. It lets VPN clients share this server's normal internet connection. Without NAT, a client may connect to the VPN but still fail to browse websites.";
            serverStatusPrerequisite.HelpText =
                "Use this after setup to check whether the server is running, clients are handshaking, bytes are moving, and DNS/NAT are healthy.";

            // -- Set up interdependencies --

            // Can't uninstall WireGuard while Tunnel is installed
            wireGuardExePrerequisite.CanConfigureFunc = () => tunnelServicePrerequisite.Fulfilled == false;

            // Can't resolve or configure server or client unless WireGuard is installed
            serverConfigurationPrerequisite.CanResolveFunc = clientConfigurationsPrerequisite.CanResolveFunc =
            serverConfigurationPrerequisite.CanConfigureFunc = clientConfigurationsPrerequisite.CanConfigureFunc = () => wireGuardExePrerequisite.Fulfilled;
            
            // Can't install tunnel until WireGuard exe is installed and server/clients are configured
            tunnelServicePrerequisite.CanResolveFunc = () =>
                wireGuardExePrerequisite.Fulfilled && serverConfigurationPrerequisite.Fulfilled && clientConfigurationsPrerequisite.Fulfilled;

            // Can't uninstall the tunnel while Windows NAT is enabled
            tunnelServicePrerequisite.CanConfigureFunc = () => internetSharingPrerequisite.Fulfilled == false;
            
            // Can't enable private network unless tunnel is installed, and private network must not be informational
            privateNetworkPrerequisite.CanResolveFunc = () => tunnelServicePrerequisite.Fulfilled &&
                                                              privateNetworkPrerequisite.IsInformational == false;

            // Can't configure private network if it's only information (e.g., on a domain)
            privateNetworkPrerequisite.CanConfigureFunc = () => privateNetworkPrerequisite.IsInformational == false;

            // Can't enable Windows NAT unless tunnel is installed
            internetSharingPrerequisite.CanResolveFunc = () => tunnelServicePrerequisite.Fulfilled;

            // Can't view server status unless tunnel is installed
            serverStatusPrerequisite.CanConfigureFunc = () => tunnelServicePrerequisite.Fulfilled;

            // Add the prereqs to the Model
            MainWindowModel mainWindowModel = new MainWindowModel();
            mainWindowModel.PrerequisiteItems.Add(wireGuardExePrerequisite);
            mainWindowModel.PrerequisiteItems.Add(serverConfigurationPrerequisite);
            mainWindowModel.PrerequisiteItems.Add(clientConfigurationsPrerequisite);
            mainWindowModel.PrerequisiteItems.Add(tunnelServicePrerequisite);
            mainWindowModel.PrerequisiteItems.Add(privateNetworkPrerequisite);
            mainWindowModel.PrerequisiteItems.Add(internetSharingPrerequisite);
            mainWindowModel.PrerequisiteItems.Add(serverStatusPrerequisite);
            mainWindowModel.RefreshSummary();

            // If one of the prereqs changes, check the validity of all of them
            mainWindowModel.PrerequisiteItems.ForEach(i => i.PropertyChanged += PrerequisiteItemFulfilledChanged);

            void PrerequisiteItemFulfilledChanged(object sender, PropertyChangedEventArgs e)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Unsubscribe before invoking on everyone
                    mainWindowModel.PrerequisiteItems.ForEach(i => i.PropertyChanged -= PrerequisiteItemFulfilledChanged);

                    Mouse.OverrideCursor = Cursors.Wait;

                    if (sender is PrerequisiteItem senderItem && e.PropertyName == nameof(PrerequisiteItem.Fulfilled))
                    {
                        // Now invoke on all but the sender
                        mainWindowModel.PrerequisiteItems.Where(i => i != senderItem).ToList().ForEach(i =>
                        {
                            i.RaisePropertyChanged(nameof(i.Fulfilled));
                            i.RaisePropertyChanged(nameof(i.IsInformational));
                            i.RaisePropertyChanged(nameof(i.CanConfigure));
                            i.RaisePropertyChanged(nameof(i.CanResolve));
                        });
                    }

                    Mouse.OverrideCursor = null;

                    mainWindowModel.RefreshSummary();

                    // Now we can resubscribe to all
                    mainWindowModel.PrerequisiteItems.ForEach(i => i.PropertyChanged += PrerequisiteItemFulfilledChanged);
                });
            }

            DataContext = mainWindowModel;

            // Check for updates
            _updateChecker = new MyUpdateChecker("https://raw.githubusercontent.com/pgnehm/Windows-Wireguard-VPN-Server-Expanded/main/WireGuardServerForWindows/VersionInfo2.xml", this);
        }

        #region Private fields

        private readonly MyUpdateChecker _updateChecker;

        #endregion

        #region Event handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto allows the user to Skip (updates are still available via F1)
            _updateChecker.CheckForUpdates(UpdateNotifyMode.Auto);
        }

        private void GuidedSetupButton_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new SetupWizardWindow
            {
                Owner = this,
                DataContext = new SetupWizardViewModel((MainWindowModel)DataContext)
            };

            wizard.ShowDialog();
        }

        #endregion

        private void AboutBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MessageBox.Show(
                this,
                $"Wireguard Server {GetType().Assembly.GetName().Version}",
                "About Wireguard Server",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
