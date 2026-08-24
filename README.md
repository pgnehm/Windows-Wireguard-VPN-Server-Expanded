
<img src="https://i.imgur.com/6XRP3gB.png" width="100" height="100" />

# Windows WireGuard VPN Server — Expanded
WS4W Expanded is a Windows desktop application for running and managing a WireGuard server endpoint.

This project is based on the original [WireGuard Server for Windows project](https://github.com/micahmo/WireGuardServerForWindows) by Micah Morrison. It retains the original application's foundation, WireGuard workflow, and MIT license while extending the project with newer Windows/.NET support, WinNAT-based routing, diagnostics, recovery, security controls, and a longer-term service architecture.

The original project was inspired by Henry Chang's post, [How to Setup WireGuard VPN Server On Windows](https://www.henrychang.ca/how-to-setup-wireguard-vpn-server-on-windows/). WS4W Expanded continues that goal of simplifying the Windows networking steps required to operate a WireGuard server, while documenting the remaining limitations instead of presenting the application as a fully automatic or anonymous relay.

## Project status

This repository is the active expanded development line for WS4W. The current application version is `1.7.0` and targets supported Windows systems with the .NET 10 Windows Desktop runtime/SDK. The current product mode is **Standard VPN**: WireGuard clients connect to this Windows host, and Windows NAT routes their IPv4 traffic through the host's normal network connection.

The project is being developed in incremental stages. This README is intentionally also a running technical status document; implementation notes, validation results, and roadmap changes should be recorded here as the project evolves.

### Current release capabilities

The current line includes:

* WireGuard installation, server and client configuration, QR-code display, and tunnel-service management.
* Configurable MTU written to generated server and client profiles and applied to the live WireGuard adapter when the tunnel is installed or updated.
* Windows NAT (WinNAT) instead of Windows Internet Connection Sharing (ICS), avoiding mass changes to unrelated adapter-sharing settings.
* Automatic WinNAT recovery through the installed `WS4WPrivileged` Windows service.
* Structured server status and network diagnostics, including handshake/traffic parsing, MTU reporting, DNS and connectivity checks, and IPv4/IPv6 state.
* Optional IPv4 kill-switch behavior, explicit DNS requirements for generated clients, IPv6-disable protection, and WireGuard-subnet-scoped firewall rules.
* DPAPI protection for private and preshared keys stored in editable WS4W data files. WireGuard runtime configuration files necessarily contain plaintext keys while the service is using them.
* CLI support for recreating the WS4W WinNAT configuration after a network-stack or adapter change.

### Current boundaries

The WPF application still requests administrator privileges for several legacy WireGuard and Windows networking operations. The privileged service currently focuses on automatic NAT recovery; the broader least-privilege service boundary is planned work.

Transparent relay/proxy behavior is **not** enabled. WS4W does not start a public SOCKS/HTTP relay, intercept traffic, or attempt to disguise a VPN as ordinary browser traffic. Adding interception without loop prevention, firewall policy, and a service boundary could create an accidental open proxy, so that work belongs after the foundation phase.

Live tunnel MTU changes still require manual validation on a machine with an active WireGuard tunnel. Automated tests cover configuration generation and the core safety logic, but they do not replace testing against the installed WireGuard driver and the host's real adapters.

### Current roadmap

The next validation milestone is an end-to-end test on a dedicated Windows machine: clean installation, WireGuard handshake, MTU `1420` and `1500`, reboot recovery, adapter disconnect/reconnect, WinNAT repair, DNS, IPv4/IPv6 behavior, diagnostics, and kill-switch behavior.

The next major foundation milestone is moving the remaining privileged WireGuard and Windows networking operations behind `WS4WPrivileged`, allowing the UI to run without administrator rights while keeping the service narrowly scoped and safe. Transparent Relay mode, WinDivert interception, and outbound proxy integration remain future work and will not be enabled until that service and firewall boundary is complete.

## Development quick start

### Requirements

* Windows 10/11 x64 with administrator access for installation and networking tests.
* .NET 10 SDK with Windows Desktop support.
* WireGuard for Windows for live tunnel tests. The application can download/install WireGuard as part of its normal workflow.
* Inno Setup 6 only when building the Windows installer.

### Build and test

From the repository root:

```powershell
dotnet restore
dotnet build WireGuardServerForWindows.sln --configuration Release
dotnet test WireGuardServerForWindows.sln --configuration Release --no-build
```

The GitHub Actions workflow in `.github/workflows/restore_build_test.yml` runs restore, build, and test on Windows. The WPF application and the service are Windows-only projects.

### Build the installer

Build the release binaries first, then compile `Installer/WS4WSetupScript.iss` with Inno Setup 6. The installer version must match the application version in `Directory.Build.props` and the corresponding version metadata under `WireGuardServerForWindows/`.

### Safe development workflow

Networking changes should be developed in this order:

1. Add or update configuration/model tests.
2. Build and run the full test suite.
3. Test the prerequisite against a disposable Windows adapter/tunnel.
4. Test failure and rollback paths, including adapter disconnects and reboot recovery.
5. Build and install a versioned installer before calling the change release-ready.

Do not test kill-switch or routing changes on a machine where loss of connectivity would be unsafe without local access.

## Architecture at a glance

The solution currently contains:

* `WireGuardServerForWindows`: WPF UI, configuration models, prerequisites, diagnostics, firewall/NAT integration, and the current privileged workflow.
* `WireGuardServerForWindows.Service`: the Windows background service used for boot-time and delayed WinNAT recovery.
* `WireGuardAPI`: process execution and WireGuard command integration.
* `WireGuardServerForWindows.Cli.Options` and `WireGuardServerForWindowsCli`: command-line options and CLI entry point.
* `WireGuardServerForWindows.Tests`: configuration, MTU, DPAPI, parsing, and safety-focused tests.

The intended long-term architecture is a non-administrator UI communicating with a narrowly scoped, administrator-owned service. Every service command should validate its inputs, use explicit executable paths, log an actionable result, and avoid accepting arbitrary command lines or arbitrary proxy destinations.

# Getting Started
Releases for this expanded project will be published on the [WS4W Expanded releases page](https://github.com/pgnehm/Windows-Wireguard-VPN-Server-Expanded/releases). Development builds can be built locally by following the [development quick start](#development-quick-start).

> **Note**: The application will request to run as Administrator. Due to all the finagling of the registry, Windows services, wg.exe calls, etc., it is easier to run the whole application elevated.

#### Upgrade from 1.5.2
Before introducing an installer, WS4W was distributed as a portable application. The portable versions (1.5.2 and earlier) have no automatic upgrade path to the installer version. To upgrade, simply delete the downloaded portable version and download the installer. No configuration settings will be lost.

# What Does It Do?

Below are the tasks that can be performed automatically using this application.

## Before
![BeforeScreenshot](https://i.imgur.com/Mlyd0TS.png)

### Download and Install WireGuard
This step downloads and runs the latest version of WireGuard for Windows from https://download.wireguard.com/windows-client/wireguard-installer.exe. Once installed, it can be uninstalled directly from WS4W, too.

### Server Configuration
![ServerConfiguration](https://user-images.githubusercontent.com/7417301/137597967-5dfcf8ba-5a22-4dcf-98f9-3aed21ae3c5e.png)

Here you can configure the server endpoint. See the WireGuard documentation for the meaning of each of these fields. The Private Key, Public Key, and Preshared Key are generated by calling `wg genkey`, `wg pubkey [private key]`, and `wg genpsk`, respectively.

> **Note**: It is important that the server's network range not conflict with the host system's IP address or LAN network range.

In addition to creating/updating the configuration file for the server endpoint, editing the server configuration also updates generated client configurations and the live WireGuard interface. The configured network range is used by Windows NAT (WinNAT) as the internal NAT prefix.

#### MTU

The server configuration includes an MTU setting. The default is `1420`, which is typical for WireGuard over a 1500-byte network because WireGuard adds encapsulation overhead. The value is written to the server configuration and to generated client configurations. It is also applied to the live Windows WireGuard adapter when the tunnel is installed or updated.

Use `1500` only when the complete path between the client and server supports it. Increasing the MTU changes packet sizing; it does not by itself remove VPN indicators from browser or TCP fingerprinting systems.

> **Important**: You must configure port forwarding on your router. Forward all UDP traffic that is destined for your server endpoint port (default `51820`) to the LAN IP of your server. Every router is different, so it is difficult to give specific guidance here. As an example, here is what the port forwarding rule would look like on a Verizon Quantum Gateway router.
> 
> ![](https://user-images.githubusercontent.com/7417301/127727564-0d666c41-4998-4c5d-8d2a-e7b730e545c8.png)

You should set the Endpoint property to your public IPv4, IPv6, or domain address, followed by whatever port you have forwarded. The `Detect Public IP Address` button will attempt to detect your public address automatically using the [ipify.org](https://ipify.org) API. However, if possible, it is recommended that you use a domain name with DDNS. That way, if your public IP address changes, your clients will be able to find your server endpoint without reconfiguration.

### Client Configuration
![ClientConfiguration](https://i.imgur.com/frxdJ7S.png)

Here you can configure the client(s). The Address can be entered manually or calculated based on the server's network range. For example, if the server's network is `10.253.0.0/24`, the client config can determine that `10.253.0.2` is a valid address. Note that the first address in the range (in this case, `10.253.0.1`) is reserved for the server. DNS is optional when DNS leak protection is disabled; otherwise each generated profile must specify at least one DNS server. Lastly, the Private Key and Public Keys are again generated using `wg genkey` and `wg pubkey [private key]`. However, the Preshared Key must match the server's. If it has already been generated in the server config, it can be automatically copied to the client config.

Once configured, it's easy to import the configuration into your client app of choice via QR code or by exporting the `.conf` file.

![ClientQrCode](https://i.imgur.com/IOIQ1Rx.png)

### Tunnnel Service
Once the server and client(s) are configured, you may install the tunnel service, which creates a new network interface for WireGuard using the `wireguard /installtunnelservice` command. After installation, the tunnel may be also removed directly within WS4W. This uses the `wireguard /uninstalltunnelservice` command.

Installing the tunnel service should be sufficient to perform a WireGuard handshake.

> **Note:** If the server configuration is edited after the tunnel service is installed, the tunnel service will automatically be updated via the `wg syncconf` command (if the newly saved server configuration is valid). This is also true of the client configurations, updates to which often cause the server configuration to be updated (e.g., if a new client is added, the server configuration must be aware of this new peer).

### Private Network
Even after the tunnel service is installed, some protocols may be blocked. It is recommended to change the network profile to `Private`, which eases Windows restrictions on the network.

> **Note**: On a system where the shared internet connection originates from a domain network, this step is not necessary, as the WireGuard interfaces picks up the profile of the shared domain network.

### Windows NAT
Windows NAT (WinNAT) provides the routing and address translation needed for connected WireGuard peers to reach the host's normal network routes. WS4W creates a named NAT object for the configured WireGuard network and enables forwarding on the `wg_server` interface.

WinNAT is stored by Windows and does not use Internet Connection Sharing's adapter-sharing state or its reboot-persistence registry workaround. No public adapter needs to be selected: Windows uses its normal route to the internet or LAN.

The installer also registers `WS4WPrivileged`, a small recovery service. It retries the WS4W NAT repair after boot and after delayed adapter initialization. Existing third-party ICS assignments are not mass-disabled or overwritten.

### Security settings
The server editor exposes three safety controls:

* `Kill switch` adds a firewall block for traffic sourced from the WireGuard subnet.
* `DNS leak protection` requires every generated client profile to contain explicit DNS servers.
* `Disable IPv6` is enabled by default because the current WinNAT path is IPv4-only. IPv6 forwarding is not silently enabled.

Private and preshared keys in the editable WS4W data files are protected with the current Windows user's DPAPI. WireGuard runtime files must still contain plaintext keys because the WireGuard service reads those files.

Firewall rules are named `WS4W-*` and restricted to the WireGuard interface/subnet. WS4W does not expose a public relay, SOCKS proxy, or transparent proxy by default.

### View Server Status
![ServerStatus](https://i.imgur.com/dcSJXKU.png)

Once the tunnel is installed, the status of the WireGuard interface may be viewed. This is accomplished via the `wg show` command. It will be continually updated as long as `Update Live` is checked.

## After
![AfterScreenshot](https://i.imgur.com/Ck5yfvj.png)

## CLI
There is also a CLI bundled in the portable download called `ws4w.exe` which can be invoked from a terminal or included in a script. In addition to messages written to standard out, the CLI will also set the exit code based on the success of executing the given command. In PowerShell, for example, the exit code can be printed with `echo $lastexitcode`.

> **Note**: The CLI must also be run as an Administrator for the same reasons as above.

### Usage
The CLI uses verbs, or top-level commands, each of which has its own set of options. You can run `ws4w.exe --help` for a list of all verbs or `ws4w.exe verb --help` to see the list of options for a particular verb.

#### List of Supported Verbs
* ```ws4w.exe restartinternetsharing [--network <LEGACY_OPTION>]```
	* This recreates the WS4W Windows NAT configuration.
	* The `--network` option is retained for script compatibility and is ignored; WinNAT uses the normal Windows route rather than a selected public adapter.
	* The exit code will be 0 if the NAT configuration was successfully recreated.
* ```ws4w.exe setpath```
    * This will tell WS4W to add the current executing directory to the system's `PATH` environment variable. It is mainly intended to be invoked by the installer but may be called manually after the fact.
    * This verb has no options.

# Known Issues
WinNAT requires a supported Windows version with the built-in NetNat PowerShell module. If another application already owns the required NAT prefix, WS4W will report the Windows error instead of changing that NAT object.

The privileged-service project is currently used for automatic NAT recovery. The WPF editor still requests elevation for the remaining legacy WireGuard/Windows operations; moving every privileged call behind a least-privilege service is a follow-up foundation step.

Transparent Relay mode is intentionally not enabled in this release. Adding WinDivert/GOST interception without the service boundary and loop/firewall policy would risk creating an accidental open proxy. The current release supports Standard VPN mode only.

### Recreating Windows NAT from Task Scheduler
The CLI can recreate the WS4W NAT object after a network stack reset or other external change. Following is an example using the Windows Task Scheduler.

1. Create a task which runs whether or not the user is logged in.
![image](https://user-images.githubusercontent.com/7417301/116771243-c457f300-aa17-11eb-9373-1b26dedfb52b.png)
2. Set the task to be triggered by system startup.
![image](https://user-images.githubusercontent.com/7417301/116771266-f0737400-aa17-11eb-99ec-7aa2ef9116a4.png)
3. Add an action that starts `ws4w.exe` with the `restartinternetsharing` verb.
![image](https://user-images.githubusercontent.com/7417301/116771293-23b60300-aa18-11eb-9070-1f2c2c0bb21d.png)
![image](https://user-images.githubusercontent.com/7417301/116771300-36c8d300-aa18-11eb-825d-28f8a74078f7.png)


# Goals
One of the more lofty goals of this project was to run a VPN behind NAT without port forwarding. I am interested by Jordan Whited's post, [WireGuard Endpoint Discovery and NAT Traversal using DNS-SD](https://www.jordanwhited.com/posts/wireguard-endpoint-discovery-nat-traversal/) and hope to investigate the possibility of integrating it into this application at some point.
