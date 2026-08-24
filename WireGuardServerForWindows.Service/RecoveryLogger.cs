using System;
using System.Diagnostics;
using System.IO;

namespace WireGuardServerForWindows.Service
{
    internal static class RecoveryLogger
    {
        private static readonly object Sync = new object();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WS4W",
            "Logs");
        private static readonly string LogPath = Path.Combine(LogDirectory, "WS4WPrivileged.log");

        public static void Info(string message) => Write("INFO", message, EventLogEntryType.Information);

        public static void Warning(string message) => Write("WARN", message, EventLogEntryType.Warning);

        public static void Error(string message) => Write("ERROR", message, EventLogEntryType.Error);

        private static void Write(string level, string message, EventLogEntryType eventType)
        {
            string line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(LogPath, line);

                    var file = new FileInfo(LogPath);
                    if (file.Length > 1_000_000)
                    {
                        string archivePath = LogPath + ".1";
                        File.Copy(LogPath, archivePath, true);
                        File.WriteAllText(LogPath, line);
                    }

                    return;
                }
                catch
                {
                    // Continue with the Windows Event Log fallback below.
                }
            }

            try
            {
                EventLog.WriteEntry("WS4WPrivileged", line, eventType);
            }
            catch
            {
                // Logging must never terminate or take down the recovery service.
            }
        }
    }
}
