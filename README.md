# Windows WireGuard VPN Server — Expanded

WS4W Expanded is a Windows application for configuring and operating a WireGuard server on a Windows host.

## Origins and attribution

This project is based on the original [WireGuard Server for Windows project](https://github.com/micahmo/WireGuardServerForWindows) by Micah Morrison. The original application provided the WireGuard server workflow, configuration model, UI foundation, and much of the initial implementation.

This repository is the expanded development line. It keeps the original project’s MIT license and attribution while adding newer Windows/.NET support, WinNAT routing, diagnostics, recovery, security controls, and a longer-term privileged-service architecture. See [LICENSE](LICENSE) for the license text.

## Current status

Current application version: **1.7.0**

Current operating mode: **Standard VPN**

The intended traffic path is:

```text
WireGuard client → WireGuard server on Windows → Windows NAT → normal host network → internet
```

This project is not currently a transparent relay, HTTP proxy, SOCKS proxy, or traffic-obfuscation tool. It does not promise to hide the fact that traffic originated through a VPN.

### Implemented

- WireGuard installation, server/client configuration, QR-code display, and tunnel-service management.
- Configurable MTU written to generated server and client configurations.
- MTU application to the live WireGuard adapter when the tunnel is installed or updated.
- Windows NAT (WinNAT) instead of mass modification of Windows Internet Connection Sharing settings.
- Automatic WinNAT recovery through the `WS4WPrivileged` Windows service.
- Structured server status and network diagnostics, including handshake, traffic, MTU, DNS, IPv4/IPv6, and internet-access checks.
- Optional IPv4 kill-switch behavior.
- DNS leak protection through explicit DNS requirements in generated client profiles.
- IPv6-disable protection because the current WinNAT path is IPv4-only.
- Firewall rules scoped to the WireGuard subnet/interface.
- DPAPI protection for private and preshared keys stored in editable WS4W data files.
- CLI support for recreating the WS4W WinNAT configuration after a network-stack or adapter change.
- GitHub Actions restore, build, and test automation.

### Current limitations

- The WPF editor still requires administrator privileges for several legacy WireGuard and Windows networking operations.
- The privileged service currently focuses on automatic NAT recovery; the complete least-privilege service boundary is not finished.
- IPv6 forwarding is not implemented. IPv6 is disabled by default rather than silently routed outside the tunnel.
- Live MTU behavior still requires validation with an active WireGuard tunnel and real network adapters.
- Transparent Relay mode, WinDivert interception, GOST integration, and automatic TCP/HTTP/SOCKS forwarding are not implemented.

## Installation and operation

### Requirements

- Windows 10/11 x64.
- Administrator access for installation and current networking operations.
- WireGuard for Windows. WS4W can download/install WireGuard as part of its workflow.
- A UDP port forwarded from the router to the Windows host, normally `51820`.
- A non-conflicting WireGuard subnet, such as `10.253.0.0/24`, that does not overlap the LAN or host network.

Installers and future release artifacts are published on the [WS4W Expanded releases page](https://github.com/pgnehm/Windows-Wireguard-VPN-Server-Expanded/releases).

After installation, the normal workflow is:

1. Install or locate WireGuard.
2. Configure the server endpoint and MTU.
3. Configure one or more clients and export their profiles or QR codes.
4. Install the WireGuard tunnel service.
5. Confirm the handshake and diagnostics.
6. Confirm that client internet traffic passes through WinNAT.

Use `1420` as the normal WireGuard-over-1500-byte starting point. Use `1500` only when the complete path supports it and the resulting tunnel has been tested for packet loss and fragmentation.

## Diagnostics and safety

The application reports or manages:

- Server/tunnel state.
- Latest client handshake.
- Bytes sent and received.
- Configured and applied MTU.
- WinNAT status and recovery results.
- DNS, IPv4, IPv6, and internet connectivity.
- Firewall and kill-switch settings.

WS4W does not disable unrelated adapter-sharing configurations and does not expose a public relay by default. Any future relay feature must include destination restrictions, loop prevention, firewall policy, and service isolation before it is enabled.

Private and preshared keys in editable WS4W data are protected with the current Windows user’s DPAPI. WireGuard runtime files still contain plaintext keys while the WireGuard service is using them.

## Development

### Requirements

- Windows 10/11 x64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with Windows Desktop support. The repository pins SDK `10.0.400` through [global.json](global.json).
- WireGuard for Windows for live tunnel testing.
- Inno Setup 6 for building the installer.

### Build and test

Run from the repository root:

```powershell
dotnet restore
dotnet build WireGuardServerForWindows.sln --configuration Release
dotnet test WireGuardServerForWindows.sln --configuration Release --no-build
```

The WPF application, service, and networking tests are Windows-specific. Automated tests do not replace testing against the installed WireGuard driver, Windows Firewall, WinNAT, and real adapters.

### Solution structure

- `WireGuardServerForWindows`: WPF UI, configuration, prerequisites, diagnostics, firewall/NAT integration, and current privileged workflow.
- `WireGuardServerForWindows.Service`: Windows service used for boot-time and delayed WinNAT recovery.
- `WireGuardAPI`: WireGuard command/process integration.
- `WireGuardServerForWindows.Cli.Options` and `WireGuardServerForWindowsCli`: CLI definitions and entry point.
- `WireGuardServerForWindows.Tests`: configuration, MTU, DPAPI, parser, and safety tests.
- `Installer`: Inno Setup installer project and release instructions.

### Installer builds

Build the Release solution first, then compile `Installer/WS4WSetupScript.iss` with Inno Setup 6. Keep the application version synchronized in `Directory.Build.props`, `VersionInfo2.xml`, and the Inno Setup script. The installer build also requires the .NET Desktop Runtime referenced by [Installer/README.md](Installer/README.md).

## Roadmap

The next milestone is field validation on a dedicated Windows machine:

1. Validate clean installation, handshake, MTU `1420`/`1500`, reboot recovery, adapter reconnect, WinNAT repair, DNS, IPv4/IPv6 behavior, diagnostics, and kill-switch behavior.
2. Complete the narrowly scoped privileged service so the UI can run without administrator rights.
3. Add robust rollback, failure reporting, and recovery for all networking changes.
4. Improve the diagnostics dashboard and recommended repair actions.
5. Add complete IPv6 support or a more explicit IPv6 policy.
6. Only after the service and firewall foundation is complete, evaluate an isolated transparent relay mode. It would begin with tightly scoped IPv4/TCP support; UDP, IPv6, DNS, and QUIC would require separate validation.

## Project principles

- Prefer Windows-native routing and NAT over modifying unrelated system settings.
- Report networking failures instead of silently discarding them.
- Keep firewall rules and service commands narrowly scoped.
- Never create an accidental open relay.
- Document what has been tested on real Windows networking hardware separately from what is covered by automated tests.
