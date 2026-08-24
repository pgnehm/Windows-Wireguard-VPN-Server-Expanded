using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace WireGuardServerForWindows.Service
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--console", StringComparison.OrdinalIgnoreCase))
            {
                using var service = new PrivilegedRecoveryService();
                service.RunOnce();
                return;
            }

            ServiceBase.Run(new PrivilegedRecoveryService());
        }
    }

    internal sealed class PrivilegedRecoveryService : ServiceBase
    {
        private const string ServiceNameValue = "WS4WPrivileged";
        private Timer _timer;

        public PrivilegedRecoveryService()
        {
            ServiceName = ServiceNameValue;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _timer = new Timer(_ => RunOnce(), null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        }

        protected override void OnStop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void RunOnce()
        {
            string application = Path.Combine(AppContext.BaseDirectory, "WireGuardServerForWindows.exe");
            if (!File.Exists(application)) return;

            try
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = application,
                    Arguments = "restartinternetsharing",
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(30_000);
            }
            catch
            {
                // The UI diagnostics dashboard reports the failed recovery. The
                // service must remain alive so the next timer tick can retry.
            }
        }
    }
}
