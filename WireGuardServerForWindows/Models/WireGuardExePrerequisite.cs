using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WireGuardAPI;
using WireGuardServerForWindows.Properties;

namespace WireGuardServerForWindows.Models
{
    public class WireGuardExePrerequisite : PrerequisiteItem
    {
        public WireGuardExePrerequisite() : base
        (
            title: Resources.WireGuardExe,
            successMessage: Resources.WireGuardExeFound,
            errorMessage: Resources.WireGuardExeNotFound,
            resolveText: Resources.InstallWireGuard,
            configureText: Resources.UninstallWireGuard
        )
        {
        }

        public override BooleanTimeCachedProperty Fulfilled => _fulfilled ??= new BooleanTimeCachedProperty(TimeSpan.FromSeconds(1), () =>
        {
            _wireGuardExe ??= new WireGuardExe();
            return _wireGuardExe.Exists;
        });
        private BooleanTimeCachedProperty _fulfilled;

        public override void Resolve()
        {
            MessageBoxResult confirmation = MessageBox.Show(
                "WireGuard is required before this app can create the VPN tunnel.\n\nDownload and start the official WireGuard for Windows installer now?",
                "Install WireGuard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            
            string downloadPath = Path.Combine(Path.GetTempPath(), "wireguard.exe");
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            byte[] installer = httpClient.GetByteArrayAsync(wireGuardExeDownload).GetAwaiter().GetResult();
            File.WriteAllBytes(downloadPath, installer);
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                Verb = "runas", // For elevation
                UseShellExecute = true // Must be true to use "runas"
            });

            Task.Run(WaitForFulfilled);

            Mouse.OverrideCursor = null;
        }

        public override void Configure()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            _wireGuardExe.ExecuteCommand(new UninstallCommand());
            Refresh();

            Mouse.OverrideCursor = null;
        }

        private readonly string wireGuardExeDownload = @"https://download.wireguard.com/windows-client/wireguard-installer.exe";
        private WireGuardExe _wireGuardExe;
    }
}
