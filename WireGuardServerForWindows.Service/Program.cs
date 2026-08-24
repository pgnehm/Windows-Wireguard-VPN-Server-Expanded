using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace WireGuardServerForWindows.Service
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--console", StringComparison.OrdinalIgnoreCase))
            {
                using var service = new PrivilegedRecoveryService();
                service.RunOnce("console");
                return;
            }

            ServiceBase.Run(new PrivilegedRecoveryService());
        }
    }

    internal sealed class PrivilegedRecoveryService : ServiceBase
    {
        private const string ServiceNameValue = "WS4WPrivileged";
        private Timer _timer;
        private int _recoveryInProgress;

        public PrivilegedRecoveryService()
        {
            ServiceName = ServiceNameValue;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            RecoveryLogger.Info("Service started. A recovery attempt will run after the network stack has initialized.");
            _timer = new Timer(_ => RunOnce("scheduled"), null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        }

        protected override void OnStop()
        {
            _timer?.Dispose();
            _timer = null;
            RecoveryLogger.Info("Service stopped.");
        }

        public void RunOnce()
        {
            RunOnce("manual");
        }

        public void RunOnce(string reason)
        {
            if (Interlocked.Exchange(ref _recoveryInProgress, 1) == 1)
            {
                RecoveryLogger.Warning($"Skipped {reason} recovery because another recovery attempt is still running.");
                return;
            }

            try
            {
                RunRecovery(reason);
            }
            finally
            {
                Volatile.Write(ref _recoveryInProgress, 0);
            }
        }

        private static void RunRecovery(string reason)
        {
            string application = Path.Combine(AppContext.BaseDirectory, "WireGuardServerForWindows.exe");
            if (!File.Exists(application))
            {
                RecoveryLogger.Error($"Skipped {reason} recovery because the application was not found at {application}.");
                return;
            }

            try
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = application,
                    Arguments = "restartinternetsharing",
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null)
                {
                    RecoveryLogger.Error($"The application process could not be started for {reason} recovery.");
                    return;
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30_000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    RecoveryLogger.Error($"The application timed out during {reason} recovery.");
                    return;
                }

                Task.WaitAll(outputTask, errorTask);
                string output = outputTask.Result.Trim();
                string error = errorTask.Result.Trim();
                if (process.ExitCode == 0)
                {
                    RecoveryLogger.Info($"Completed {reason} recovery successfully.{FormatOutput(output)}");
                }
                else
                {
                    RecoveryLogger.Error($"{reason} recovery failed with exit code {process.ExitCode}.{FormatOutput(error)}");
                }
            }
            catch (Exception exception)
            {
                RecoveryLogger.Error($"{reason} recovery threw an exception: {exception.Message}");
            }
        }

        private static string FormatOutput(string output)
        {
            return string.IsNullOrWhiteSpace(output) ? string.Empty : $" Output: {output.Replace(Environment.NewLine, " | ")}";
        }
    }
}
