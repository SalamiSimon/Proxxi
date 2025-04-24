# Proxxi - HTTP Traffic Interceptor and Modifier

Proxxi is a Windows application built using WinUI 3 that allows you to intercept and modify HTTP/HTTPS traffic. It provides a user-friendly interface for configuring traffic interception rules.

## Requirements

- Windows 10 version 1809 (build 17763) or later
- [Windows App SDK Runtime](https://aka.ms/windowsappsdk/1.5/1.5.240227000/windowsappruntimeinstall-x64.exe)
- .NET 8 Desktop Runtime

## Installation

The application can be installed using the MSI installer located in the `ProxxiSetup\bin\Release\` directory. This installer includes all necessary runtime components.

### Important: Windows App SDK Installation

WinUI 3 applications require the Windows App SDK Runtime to be installed. There are two ways to handle this:

1. **Preferred Method**: Download and install the Windows App SDK Runtime manually from [here](https://aka.ms/windowsappsdk/1.5/1.5.240227000/windowsappruntimeinstall-x64.exe) before running the application. This is the most reliable approach.

2. **Alternative Method**: Our application includes scripts that can attempt to download and install the Windows App SDK Runtime automatically, but this might not work in all environments due to security restrictions.

### Installation Steps

1. Install the application using the MSI installer
2. Manually install the Windows App SDK Runtime if not already installed
3. Run the application using the shortcuts or directly from the installation folder

## Troubleshooting WinUI 3 Application Launch Issues

If you're experiencing issues launching the application, try the following:

1. **Manually Install Windows App SDK Runtime**: This is the most important step for WinUI 3 applications. Download and run the installer from [here](https://aka.ms/windowsappsdk/1.5/1.5.240227000/windowsappruntimeinstall-x64.exe).

2. **Use the LaunchApp.bat file**: The installer includes a launcher script that properly initializes the Windows App SDK runtime components before launching the application. You can find this file in the installation directory.

3. **Run the diagnostic script**: Run the `DiagnoseWinUI.ps1` PowerShell script included in the installation directory to check if all dependencies are properly installed.

4. **Run as Administrator**: In some cases, running the application as administrator might help, especially during the first launch.

5. **Common issues**:
   - Our detection script might not correctly identify the Windows App SDK even when it's installed. If you've manually installed it but the script still says it's not found, try running the application directly.
   - Different Windows versions might store the Windows App SDK files in different locations
   - In some cases, Windows security settings might prevent the application from running
   - The initialization of Windows App SDK components might fail, but the application could still work if the SDK is properly installed

## FAQ

### Q: I installed Windows App SDK Runtime manually, but the LaunchApp.bat script still says it's not installed. What should I do?
A: This is a known issue. The script might not detect all installation types correctly. If you've installed the SDK manually, try running the application directly from the installation folder instead of using the launcher script.

### Q: How can I verify that Windows App SDK Runtime is properly installed?
A: Run the `DiagnoseWinUI.ps1` script which will check various possible locations where the SDK might be installed.

### Q: Do all users need to install Windows App SDK Runtime separately?
A: Yes, the Windows App SDK Runtime is a system-wide dependency required by all WinUI 3 applications. It needs to be installed once per machine, similar to how .NET Runtime is installed.

## Developer Information

When building the application or making changes, keep the following in mind:

1. The application uses the Windows App SDK for its UI
2. The application uses the MITM Modular Python package for traffic interception
3. The installer is built using WiX Toolset 3.14

## Building from Source

1. Ensure you have the .NET 8 SDK installed
2. Ensure you have the Windows App SDK installed
3. Build the solution using Visual Studio or the `dotnet build` command
4. To create the installer, run the `build.bat` script in the `ProxxiSetup` directory 