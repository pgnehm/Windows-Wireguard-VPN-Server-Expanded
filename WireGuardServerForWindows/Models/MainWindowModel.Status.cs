using System.Linq;

namespace WireGuardServerForWindows.Models
{
    public partial class MainWindowModel
    {
        public int RequiredCount => PrerequisiteItems.Count(i => !i.IsInformational.Value);

        public int CompletedCount => PrerequisiteItems.Count(i => !i.IsInformational.Value && i.Fulfilled.Value);

        public bool IsReady => RequiredCount > 0 && CompletedCount == RequiredCount;

        public string StatusTitle => IsReady ? "VPN server is ready" : "Finish the setup to start the VPN server";

        public string StatusSummary => IsReady
            ? "WireGuard and the required Windows networking steps are complete."
            : $"{CompletedCount} of {RequiredCount} required steps complete. Use Fix or Configure on the step that needs attention.";

        public void RefreshSummary()
        {
            RaisePropertyChanged(nameof(RequiredCount));
            RaisePropertyChanged(nameof(CompletedCount));
            RaisePropertyChanged(nameof(IsReady));
            RaisePropertyChanged(nameof(StatusTitle));
            RaisePropertyChanged(nameof(StatusSummary));
        }
    }
}
