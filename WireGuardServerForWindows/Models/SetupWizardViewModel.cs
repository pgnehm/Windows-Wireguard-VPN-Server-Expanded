using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace WireGuardServerForWindows.Models
{
    public class SetupWizardViewModel : ObservableObject
    {
        public SetupWizardViewModel(MainWindowModel mainWindowModel)
        {
            _mainWindowModel = mainWindowModel;
            Steps = mainWindowModel.PrerequisiteItems
                .Select(CreateStep)
                .ToList();
        }

        public IReadOnlyList<SetupWizardStep> Steps { get; }

        public SetupWizardStep CurrentStep => Steps[CurrentIndex];

        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (Set(nameof(CurrentIndex), ref _currentIndex, value))
                {
                    RaiseStepPropertiesChanged();
                }
            }
        }
        private int _currentIndex;

        public string StepCounter => $"Step {CurrentIndex + 1} of {Steps.Count}";

        public bool CanGoBack => CurrentIndex > 0;

        public bool CanGoNext => CurrentIndex < Steps.Count - 1;

        public string PrimaryActionText => CurrentStep.PrerequisiteItem.Fulfilled
            ? CurrentStep.PrerequisiteItem.ConfigureText
            : CurrentStep.PrerequisiteItem.ResolveText;

        public ICommand BackCommand => _backCommand ??= new RelayCommand(() => CurrentIndex--, () => CanGoBack);
        private RelayCommand _backCommand;

        public ICommand NextCommand => _nextCommand ??= new RelayCommand(() => CurrentIndex++, () => CanGoNext);
        private RelayCommand _nextCommand;

        public ICommand RunCurrentStepCommand => _runCurrentStepCommand ??= new RelayCommand(RunCurrentStep);
        private RelayCommand _runCurrentStepCommand;

        public void Refresh()
        {
            _mainWindowModel.RefreshSummary();
            RaiseStepPropertiesChanged();
        }

        private void RunCurrentStep()
        {
            PrerequisiteItem prerequisite = CurrentStep.PrerequisiteItem;

            if (prerequisite.Fulfilled)
            {
                if (prerequisite.CanConfigure)
                {
                    prerequisite.Configure();
                }
            }
            else if (prerequisite.CanResolve)
            {
                prerequisite.Resolve();
            }

            Refresh();
        }

        private void RaiseStepPropertiesChanged()
        {
            RaisePropertyChanged(nameof(CurrentStep));
            RaisePropertyChanged(nameof(StepCounter));
            RaisePropertyChanged(nameof(CanGoBack));
            RaisePropertyChanged(nameof(CanGoNext));
            RaisePropertyChanged(nameof(PrimaryActionText));
            _backCommand?.RaiseCanExecuteChanged();
            _nextCommand?.RaiseCanExecuteChanged();
        }

        private static SetupWizardStep CreateStep(PrerequisiteItem prerequisite)
        {
            string title = prerequisite.Title;
            string instructions = title switch
            {
                "WireGuard.exe" =>
                    "WireGuard is the VPN engine and Windows network driver. This app configures it, but WireGuard creates the actual encrypted tunnel. If it is missing, the button below can download the official WireGuard installer after you confirm.",
                "Server Configuration" =>
                    "These are the settings clients need to reach this server. Endpoint format is host:port, for example 70.226.22.201:51820 or vpn.example.com:51820. If the server is behind a home router, you usually need to forward UDP traffic from the router to this PC. MTU is the largest packet size the VPN uses; 1420 is safest, and 1500 should only be tested after the VPN works.",
                "Client Configuration(s)" =>
                    "Create one client profile for each phone, laptop, or computer that will connect. Normally you only enter a device name, save, then export a config file or QR code for that specific device. Address, DNS, and keys are advanced because the app can fill them in.",
                "Tunnel Service" =>
                    "A tunnel is the encrypted network path between a client and this server. The tunnel service starts WireGuard in the background using the saved server and client configuration, so it is installed after those settings exist.",
                "Private Network" =>
                    "Windows treats Public networks as untrusted and blocks more traffic. The WireGuard adapter should usually be Private so server-side VPN/NAT traffic works without loosening the real public internet adapter.",
                "Windows NAT" =>
                    "NAT means Network Address Translation. It lets several VPN clients share this server's normal internet connection. Without NAT, clients may connect to the tunnel but fail to reach websites.",
                "Server Status" =>
                    "Use this screen after setup to confirm the server is running, clients are handshaking, traffic is moving, and DNS/NAT checks look healthy.",
                _ => prerequisite.HelpText
            };

            string actionText = title switch
            {
                "WireGuard.exe" => "Click the button to download and start the official WireGuard installer. After it finishes, return here and this step should turn green.",
                "Server Configuration" => "Review the defaults, confirm the public endpoint, set up router port forwarding if this PC is behind a router, then save.",
                "Client Configuration(s)" => "Click Add Client, enter a personal device name, save, then generate a QR code or export a config file for that device.",
                "Tunnel Service" => "Install this after server and client settings are saved. WireGuard needs those files before Windows can start the background tunnel.",
                "Private Network" => "Make the WireGuard network private if Windows requires it.",
                "Windows NAT" => "Enable Windows NAT so VPN clients can reach the internet.",
                "Server Status" => "Open status and verify handshake, bytes, DNS, NAT, and internet access.",
                _ => "Complete this step, then return to the wizard."
            };

            return new SetupWizardStep(prerequisite, instructions, actionText);
        }

        private readonly MainWindowModel _mainWindowModel;
    }
}
