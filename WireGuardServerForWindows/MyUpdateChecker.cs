using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace WireGuardServerForWindows
{
    public enum UpdateNotifyMode
    {
        Auto
    }

    /// <summary>
    /// Small native-WPF update checker. This replaces the old UI helper package
    /// so the application does not need its legacy .NET Framework dependency tree.
    /// </summary>
    public sealed class MyUpdateChecker
    {
        private readonly string _url;
        private readonly Window _owner;

        public MyUpdateChecker(string url, Window owner = null)
        {
            _url = url;
            _owner = owner;
        }

        public async void CheckForUpdates(UpdateNotifyMode mode)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string xml = await client.GetStringAsync(_url);
                var document = XDocument.Parse(xml);
                string versionText = document.Root?.Element("Version")?.Value;
                string downloadLink = document.Root?.Element("DownloadLink")?.Value;
                if (!Version.TryParse(versionText, out Version availableVersion)
                    || !Version.TryParse(Assembly.GetExecutingAssembly().GetName().Version?.ToString(), out Version currentVersion)
                    || availableVersion <= currentVersion
                    || string.IsNullOrWhiteSpace(downloadLink))
                {
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    _owner,
                    $"WS4W {availableVersion} is available. Open the download page?",
                    "Update available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo { FileName = downloadLink, UseShellExecute = true });
                }
            }
            catch
            {
                // Update checks are optional and must never prevent the VPN UI
                // from opening when the update endpoint is unavailable.
            }
        }
    }
}
