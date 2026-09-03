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
                    "WireGuard is the VPN engine. This app configures it, but WireGuard itself provides the tunnel driver. If this step is not complete, install WireGuard first.",
                "Server Configuration" =>
                    "These are the main settings clients need in order to reach this server. The app now fills in a friendly name, detects the public IP address, generates missing keys, and saves a Desktop backup when you save.",
                "Client Configuration(s)" =>
                    "Create one client profile for each phone, laptop, or computer that will connect. Do not share one profile across several devices. After saving a client, export its file or QR code and import it into the WireGuard app on that device.",
                "Tunnel Service" =>
                    "This starts WireGuard in the background on the Windows server. After this is installed, the VPN can keep running even when the setup window is closed.",
                "Private Network" =>
                    "Windows should treat the WireGuard network as private so the local server-side networking rules are not overly restrictive. If this machine is joined to a domain, this step may only show information.",
                "Windows NAT" =>
                    "Windows NAT lets connected VPN clients use this server's normal internet connection. Without this, clients may connect to the server but fail to browse the web.",
                "Server Status" =>
                    "Use this screen after setup to confirm the server is running, clients are handshaking, traffic is moving, and DNS/NAT checks look healthy.",
                _ => prerequisite.HelpText
            };

            string actionText = title switch
            {
                "WireGuard.exe" => "Install or locate WireGuard, then come back to this wizard.",
                "Server Configuration" => "Review the defaults, confirm the endpoint and port, then save.",
                "Client Configuration(s)" => "Add a client, save it, then export a config file or QR code for that device.",
                "Tunnel Service" => "Install the tunnel service after server and client configuration are complete.",
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
