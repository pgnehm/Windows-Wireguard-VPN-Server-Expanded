using System;
using System.Text.RegularExpressions;

namespace WireGuardServerForWindows.Models
{
    public sealed class WireGuardStatusSnapshot
    {
        public bool IsRunning { get; init; }
        public string LastClientHandshake { get; init; } = "No handshake recorded";
        public string BytesReceived { get; init; } = "Unknown";
        public string BytesSent { get; init; } = "Unknown";
        public int PeerCount { get; init; }
        public string Error { get; init; }
    }

    /// <summary>
    /// Parses human-readable <c>wg show</c> output so the dashboard can expose
    /// useful fields without displaying the raw command output.
    /// </summary>
    public static class WireGuardStatusParser
    {
        private static readonly Regex HandshakeRegex = new Regex(
            @"latest handshake:\s*(?<value>[^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TransferRegex = new Regex(
            @"transfer:\s*(?<received>[^,\r\n]+) received,\s*(?<sent>[^\r\n]+) sent",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static WireGuardStatusSnapshot Parse(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return new WireGuardStatusSnapshot
                {
                    IsRunning = false,
                    Error = "The WireGuard interface is stopped or wg.exe returned no status."
                };
            }

            Match handshake = HandshakeRegex.Match(output);
            Match transfer = TransferRegex.Match(output);
            int peerCount = Regex.Matches(output, @"^peer:", RegexOptions.Multiline | RegexOptions.IgnoreCase).Count;

            return new WireGuardStatusSnapshot
            {
                IsRunning = output.IndexOf("interface:", StringComparison.OrdinalIgnoreCase) >= 0,
                LastClientHandshake = handshake.Success ? handshake.Groups["value"].Value.Trim() : "No handshake recorded",
                BytesReceived = transfer.Success ? transfer.Groups["received"].Value.Trim() : "Unknown",
                BytesSent = transfer.Success ? transfer.Groups["sent"].Value.Trim() : "Unknown",
                PeerCount = peerCount
            };
        }
    }
}
