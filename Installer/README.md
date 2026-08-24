# WS4W Expanded Installer

This installer packages the current Windows WireGuard VPN Server - Expanded build. The project is based on the original WS4W work by Micah Morrison; see the repository [README](../README.md) and [LICENSE](../LICENSE) for attribution.

## Prerequisite

Download and install [Inno Setup](https://jrsoftware.org/isinfo.php).

Download the [.NET 10 Desktop Runtime (v10.0.11)](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-10.0.11-windows-x64-installer) and place `windowsdesktop-runtime-10.0.11-win-x64.exe` in `WireGuardServerForWindows\Installer`.

## Generate Installer for New Version

Open the main `WireGuardServerForWindows.sln` in Visual Studio.
* Change the build configuration to Release.
* Edit `WireGuardServerForWindows\VersionInfo2.xml` to include the latest version, release date, and download path.
* Bump assembly versions in `Directory.Build.props`.
* Rebuild the solution

> It's probably a good idea to commit at this point so that the installer is generated from committed code.

Open `WS4WSetupScript.iss` in Inno Setup.
* Bump the `MyAppVersion` preprocessor definition.
* Compile.

Create a new release on GitHub and upload the installer.
