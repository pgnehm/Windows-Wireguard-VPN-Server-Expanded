# Wireguard Server

Wireguard Server is a Windows application for configuring and operating a WireGuard server on a Windows host.

## Origins and attribution

This project is based on the original [WireGuard Server for Windows project](https://github.com/micahmo/WireGuardServerForWindows) by Micah Morrison. The original application provided the WireGuard server workflow, configuration model, UI foundation, and much of the initial implementation.

This repository is the expanded development line. It keeps the original project’s MIT license and attribution while adding newer Windows/.NET support, WinNAT routing, diagnostics, recovery, security controls, and a longer-term privileged-service architecture. See [LICENSE](LICENSE) for the license text.

## Current status

Current application version: **1.7.3**

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
- DPAPI protection for private and preshared keys stored in editable application data files.
- Plain-language server configuration guidance, automatic public IP detection, automatic missing-key generation, and annotated Desktop backups after saving server settings.
- CLI support for recreating the WS4W WinNAT configuration after a network-stack or adapter change.
- GitHub Actions restore, build, and test automation.

### Current limitations

- The WPF editor still requires administrator privileges for several legacy WireGuard and Windows networking operations.
- The privileged service currently focuses on automatic NAT recovery; the complete least-privilege service boundary is not finished.
- IPv6 forwarding is not implemented. IPv6 is disabled by default rather than silently routed outside the tunnel.
- Live MTU behavior still requires validation with an active WireGuard tunnel and real network adapters.
- Transparent Relay mode, WinDivert interception, GOST integration, and automatic TCP/HTTP/SOCKS forwarding are not implemented.

## Setup guide

This section is written for someone setting up a small Windows WireGuard server at home, in an office, or on a mini PC. The app still performs Windows networking changes, so expect to run it as administrator for now.

### What you need before installing

- A Windows 10 or Windows 11 x64 computer that will stay powered on while you want the VPN available.
- Administrator access on that Windows computer.
- A working internet connection on the Windows computer, preferably wired Ethernet.
- Access to the router or firewall in front of the Windows computer.
- A public IP address, dynamic DNS name, or other hostname that VPN clients can reach from outside your network.
- One UDP port forwarded from the router to the Windows computer. The normal WireGuard port is `51820`.
- A WireGuard subnet that does not overlap your normal home or office network. `10.253.0.0/24` is a reasonable starting value.

Example: if your home LAN is `192.168.1.x`, do not use `192.168.1.x` for WireGuard. Use something separate, such as `10.253.0.x`.

### Recommended server preparation

Before installing Wireguard Server, prepare the Windows machine:

1. Install Windows updates and reboot.
2. Give the server a stable LAN address. A router DHCP reservation is usually easier than manually setting a static IP in Windows.
3. Confirm the server can browse the internet.
4. Confirm the router forwards UDP port `51820` to the server's LAN address.
5. If the server is behind a changing residential IP address, set up dynamic DNS and use that DNS name as the VPN endpoint.
6. Disable sleep or hibernation if this machine should act as an always-on VPN server.
7. If the BIOS supports it, enable auto-start after power loss for unattended recovery.

### Install Wireguard Server

Installers and future release artifacts are published on the [Wireguard Server releases page](https://github.com/pgnehm/Windows-Wireguard-VPN-Server-Expanded/releases).

1. Download the latest `WireguardServerSetup-*.exe` installer from the releases page.
2. Right-click the installer and choose `Run as administrator`.
3. Follow the installer prompts.
4. Allow the installer to install the required .NET Desktop Runtime if prompted.
5. Start `Wireguard Server`.
6. If Windows asks for administrator permission, approve it.

The installer also registers the `WS4WPrivileged` Windows service. That service is used for boot-time recovery of the Windows NAT configuration.

### Install or locate WireGuard

Wireguard Server needs WireGuard for Windows because WireGuard provides the actual VPN tunnel driver and command-line tools.

1. Open Wireguard Server.
2. Find the `WireGuard.exe` requirement.
3. If WireGuard is missing, use the app's install/download action.
4. After WireGuard installs, return to Wireguard Server and refresh the requirement status if needed.

### Configure the server

Use the app's server configuration screen to set the main VPN details:

- `Endpoint`: the public hostname or public IP address clients will use to reach this server.
- `Listen port`: normally `51820`.
- `WireGuard network`: a private network used only by VPN clients, such as `10.253.0.0/24`.
- `Server address`: the server's VPN address inside that network, commonly `10.253.0.1`.
- `DNS`: the DNS server clients should use while connected. Use a DNS server you trust and can reach through the tunnel.
- `MTU`: use `1420` first. Try `1500` only after the tunnel works and you can test for packet loss or broken websites.

Plain-language MTU guidance:

- `1420` is the safer default for WireGuard on normal Ethernet/internet paths.
- `1500` may make fingerprinting look different in some tests, but it can break or slow traffic if any network path cannot carry packets that large after WireGuard overhead.
- If websites load partially, video calls behave strangely, or large downloads stall after changing MTU, go back to `1420`.

### Add clients

Create one client profile per device. Do not reuse the same client profile on multiple devices at the same time.

1. Add a new client in Wireguard Server.
2. Give it a clear name, such as `Pat-phone`, `Pat-laptop`, or `Travel-mini`.
3. Let the app generate keys and addresses.
4. Save the configuration.
5. Export the client configuration or display the QR code.
6. Import the profile into the WireGuard app on the client device.

For phones, scanning the QR code is usually easiest. For laptops, exporting a `.conf` file and importing it into WireGuard is usually easiest.

### Start the VPN server

After the server and at least one client are configured:

1. Install the WireGuard tunnel service from inside Wireguard Server.
2. Enable Windows NAT from inside Wireguard Server.
3. Confirm the diagnostics dashboard shows the server as running.
4. Connect one client from outside the server's local network.
5. Confirm the dashboard shows a recent handshake.
6. Confirm bytes sent and received increase while the client browses the web.

The preferred outside test is a phone on cellular data with Wi-Fi turned off. Testing from the same LAN can be misleading because some routers do not support loopback to their own public address.

### Confirm it is working

On the client device:

1. Connect the WireGuard profile.
2. Visit a site that shows your public IP address.
3. Confirm the public IP matches the server's internet connection, not the client's original network.
4. Browse a few normal websites.
5. Run a speed test if performance matters.
6. Disconnect WireGuard and confirm normal client internet access returns.

In Wireguard Server:

- `Server running/stopped` should show running.
- `Last client handshake` should update after the client connects.
- `Bytes sent/received` should increase while the client uses the internet.
- `MTU currently applied` should match the configured MTU.
- `Internet sharing status` should show the Windows NAT path is enabled.
- DNS and IPv4 checks should be healthy.

### Reboot test

Do this once before relying on the server remotely:

1. Leave the tunnel and Windows NAT enabled.
2. Reboot the Windows server.
3. Open Wireguard Server after Windows starts.
4. Confirm the server is running.
5. Confirm Windows NAT recovered.
6. Connect a client from outside the LAN.
7. Confirm handshake, DNS, and internet access still work.

If NAT does not recover after reboot, check the `WS4WPrivileged` service in Windows Services and review the app diagnostics.

### Common setup problems

- No handshake: check the public endpoint, router port forward, Windows Firewall, and whether the client is testing from outside the LAN.
- Handshake works but no internet: check Windows NAT status, server internet access, DNS settings, and the selected WireGuard subnet.
- Some sites load but others fail: try MTU `1420`.
- Client has DNS issues: use an explicit DNS server in the client profile and confirm that server is reachable through the tunnel.
- VPN works until reboot: check that the `WS4WPrivileged` service is installed and set to delayed automatic start.
- Server IP changed on the LAN: update the router port forward or create a DHCP reservation.

### Updating

1. Download the newer installer from the releases page.
2. Save or export any important client profiles before upgrading.
3. Run the installer as administrator.
4. Open Wireguard Server.
5. Confirm the tunnel, NAT, firewall, DNS, and client handshake checks are still healthy.

### Uninstalling

Before uninstalling, disconnect clients and remove the tunnel from inside Wireguard Server if possible. Then uninstall from Windows `Installed apps`.

After uninstalling, check WireGuard for Windows if you want to remove the WireGuard client application itself. Wireguard Server and WireGuard for Windows are separate applications.

## Diagnostics and safety

The application reports or manages:

- Server/tunnel state.
- Latest client handshake.
- Bytes sent and received.
- Configured and applied MTU.
- WinNAT status and recovery results.
- DNS, IPv4, IPv6, and internet connectivity.
- Firewall and kill-switch settings.

Wireguard Server does not disable unrelated adapter-sharing configurations and does not expose a public relay by default. Any future relay feature must include destination restrictions, loop prevention, firewall policy, and service isolation before it is enabled.

Private and preshared keys in editable application data are protected with the current Windows user’s DPAPI. WireGuard runtime files still contain plaintext keys while the WireGuard service is using them.

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
