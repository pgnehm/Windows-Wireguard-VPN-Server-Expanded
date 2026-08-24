using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommandLine;
using WireGuardServerForWindows.Cli.Options;
using WireGuardServerForWindows.Controls;
using WireGuardServerForWindows.Models;

namespace WireGuardServerForWindows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Any())
            {
                Parser.Default.ParseArguments<RestartInternetSharingCommand, SetPathCommand>(e.Args)
                    .WithParsed<RestartInternetSharingCommand>(RestartInternetSharing)
                    .WithParsed<SetPathCommand>(SetPath);

                // Don't proceed to GUI if started with command-line args
                Environment.Exit(0);
            }

            StartWindowsNatRecovery();
        }

        private void StartWindowsNatRecovery()
        {
            int attempts = 0;
            var recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            recoveryTimer.Tick += (_, __) =>
            {
                attempts++;
                var prerequisite = new InternetSharingPrerequisite();
                string recoveryError;
                if (!prerequisite.TryRecover(out recoveryError) && !string.IsNullOrEmpty(recoveryError))
                {
                    Trace.WriteLine($"WS4W NAT recovery attempt {attempts} failed: {recoveryError}");
                }

                // WinNAT itself is persistent. A few delayed attempts cover the
                // common case where the WireGuard adapter appears after logon.
                if (attempts >= 6)
                {
                    recoveryTimer.Stop();
                }
            };
            recoveryTimer.Start();
        }

        private static void RestartInternetSharing(RestartInternetSharingCommand o)
        {
            var internetSharingPrerequisite = new InternetSharingPrerequisite();

            if (internetSharingPrerequisite.Fulfilled)
            {
                // WinNAT is already enabled. Remove it before recreating it.
                Console.WriteLine(WireGuardServerForWindows.Properties.Resources.DisablingInternetSharing);
                internetSharingPrerequisite.Configure();
            }

            Console.WriteLine(WireGuardServerForWindows.Properties.Resources.EnablingInternetSharing, "Windows NAT");
            internetSharingPrerequisite.Resolve(o.NetworkToShare);

            int result = internetSharingPrerequisite.Fulfilled ? 0 : 1;

            Environment.Exit(result);
        }

        private static void SetPath(SetPathCommand o)
        {
            string pathEnvVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

            if (string.IsNullOrEmpty(pathEnvVar))
            {
                Console.WriteLine(Cli.Options.Properties.Resources.CantLoadPath);
                Environment.Exit(1);
            }

            string pwd = AppContext.BaseDirectory;

            if (string.IsNullOrEmpty(pwd))
            {
                Console.WriteLine(Cli.Options.Properties.Resources.CantLoadPwd);
                Environment.Exit(1);
            }

            if (pathEnvVar.Contains(pwd) == false)
            {
                pathEnvVar = $"{pathEnvVar};{pwd}";
                Environment.SetEnvironmentVariable("PATH", pathEnvVar, EnvironmentVariableTarget.Machine);
                Console.WriteLine(Cli.Options.Properties.Resources.AddedPwdToPath, pwd);
            }
            else
            {
                Console.WriteLine(Cli.Options.Properties.Resources.FoundPwdInPath, pwd);
            }
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // In case something was in progress when the error occurred
            Mouse.OverrideCursor = null;

            Exception realException = e.Exception;
            while (realException.InnerException is { } innerException)
            {
                realException = innerException;
            }

            new UnhandledErrorWindow {DataContext = new UnhandledErrorWindowModel
            {
                Title = WireGuardServerForWindows.Properties.Resources.Error,
                Text = string.Format(WireGuardServerForWindows.Properties.Resources.UnexpectedErrorMessage, realException.Message),
                Exception = e.Exception
            }}.ShowDialog();


            // Don't kill the app
            e.Handled = true;
        }
    }
}
