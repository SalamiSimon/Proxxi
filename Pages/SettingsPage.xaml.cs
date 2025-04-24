using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Management;
using WinUI_V3.Helpers;

namespace WinUI_V3.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // Paths relative to the application directory
        private readonly string ModularPath;
        private readonly string RootModularPath;
        private readonly string PythonPath;
        
        // Process for the proxy server
        private Process? _proxyProcess = null;
        
        // Settings
        private bool _showMitmproxyLogs = false;
        
        public SettingsPage()
        {
            // Initialize paths relative to the application directory
            string appDirectory = GetAppFolder();
            string toolsPath = Path.Combine(appDirectory, "tools");
            
            RootModularPath = toolsPath;
            ModularPath = Path.Combine(toolsPath, "mitm_modular");
            PythonPath = Path.Combine(toolsPath, "python", "Scripts");
            
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }
        
        // Get application folder (same as in TargetsPage)
        private static string GetAppFolder()
        {
            try
            {
                // Get the application's executable path
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath == null)
                {
                    Debug.WriteLine("Unable to get executable path, falling back to current directory");
                    return Directory.GetCurrentDirectory();
                }
                
                string? exeDir = Path.GetDirectoryName(exePath);
                if (exeDir == null)
                {
                    Debug.WriteLine("Unable to get directory of executable, falling back to current directory");
                    return Directory.GetCurrentDirectory();
                }
                
                Debug.WriteLine($"Application path: {exeDir}");
                return exeDir;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting app folder: {ex.Message}");
                // Fallback to current directory
                return Directory.GetCurrentDirectory();
            }
        }
        
        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize proxy toggle state based on whether proxy is running
                ProxyToggle.Toggled -= ProxyToggle_Toggled; // Prevent event firing during init
                ProxyToggle.IsOn = await ProxyService.IsProxyRunning();
                ProxyToggle.Toggled += ProxyToggle_Toggled;
                
                // Load the ShowLogs setting value (could be from settings storage in the future)
                _showMitmproxyLogs = false; // default to false
                ShowLogsToggle.IsOn = _showMitmproxyLogs;
                
                // Check if we need to verify dependencies (first launch or missing dependencies)
                bool shouldCheckDependencies = await AppService.ShouldCheckDependencies();
                if (shouldCheckDependencies)
                {
                    // Dependencies might be missing, show installation dialog
                    bool installDependencies = await DialogService.ShowConfirmationDialog(
                        this.XamlRoot,
                        "Dependencies Required",
                        "Python and/or mitmproxy are not installed. Would you like to install them now?",
                        "Install",
                        "Cancel"
                    );
                    
                    if (installDependencies)
                    {
                        // Show progress indicator
                        ProgressRing.IsActive = true;
                        ProgressContainer.Visibility = Visibility.Visible;
                        StatusText.Text = "Installing dependencies...";
                        
                        // Install dependencies - now returns success status and logs
                        var installResult = await DependencyService.InstallDependencies();
                        
                        // Hide progress indicator
                        ProgressRing.IsActive = false;
                        ProgressContainer.Visibility = Visibility.Collapsed;
                        
                        if (installResult.Success)
                        {
                            await DialogService.ShowInfoDialog(
                                this.XamlRoot,
                                "Installation Complete",
                                "Required dependencies have been installed successfully."
                            );
                        }
                        else
                        {
                            // Show an error dialog with the option to view detailed logs
                            var result = await DialogService.ShowDialog(
                                this.XamlRoot,
                                "Installation Failed",
                                "Failed to install dependencies. You can view the installation logs for more details.",
                                "View Logs",
                                "Try Again",
                                "Close"
                            );
                            
                            // Handle user's choice
                            if (result == ContentDialogResult.Primary)
                            {
                                // User wants to view logs
                                await DialogService.ShowLogDialog(
                                    this.XamlRoot,
                                    "Installation Logs",
                                    installResult.Logs
                                );
                            }
                            else if (result == ContentDialogResult.Secondary)
                            {
                                // User wants to try again
                                await RetryDependencyInstallation();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SettingsPage_Loaded: {ex.Message}");
            }
        }
        
        // Helper method to check if a process is our mitmdump process
        private bool IsMitmdumpProcess(Process process)
        {
            try
            {
                if (process.MainModule?.FileName != null)
                {
                    string processPath = process.MainModule.FileName;
                    // Check if this is our embedded Python running mitmdump
                    if (processPath.Contains("python") && 
                        (processPath.Contains(PythonPath) || processPath.Contains("Scripts\\mitmdump")))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking process: {ex.Message}");
                return false;
            }
        }
        
        private async Task<bool> IsProxyRunning()
        {
            try
            {
                // Check if the proxy is running by looking for mitmdump processes
                var processes = Process.GetProcessesByName("mitmdump");
                var pythonProcesses = Process.GetProcessesByName("python");
                
                // Check if any of the known processes match our criteria
                foreach (var process in processes)
                {
                    try
                    {
                        if (IsMitmdumpProcess(process))
                        {
                            process.Dispose();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking mitmdump process: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Also check python processes that might be running our mitmdump
                foreach (var process in pythonProcesses)
                {
                    try 
                    {
                        if (IsMitmdumpProcess(process))
                        {
                            process.Dispose();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking python process: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Check if the proxy process reference is still valid
                if (_proxyProcess != null && !_proxyProcess.HasExited)
                {
                    return true;
                }
                
                await Task.CompletedTask; // Add an await operation to make this truly async
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if proxy is running: {ex.Message}");
                await Task.CompletedTask; // Add an await operation to make this truly async
                return false;
            }
        }
        
        private async void ProxyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ProxyToggle.IsOn)
                {
                    // Start the proxy
                    await StartProxy();
                }
                else
                {
                    // Stop the proxy
                    await StopProxy();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling proxy: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Proxy Error", 
                    $"Failed to {(ProxyToggle.IsOn ? "start" : "stop")} the proxy: {ex.Message}"
                );
                
                // Reset the toggle without triggering the event
                ProxyToggle.Toggled -= ProxyToggle_Toggled;
                ProxyToggle.IsOn = !ProxyToggle.IsOn;
                ProxyToggle.Toggled += ProxyToggle_Toggled;
            }
        }
        
        private async void ShowLogsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                _showMitmproxyLogs = ShowLogsToggle.IsOn;
                
                // If the proxy is currently running, restart it to apply the new setting
                if (ProxyToggle.IsOn)
                {
                    // Briefly disable the toggle to prevent interference
                    ProxyToggle.Toggled -= ProxyToggle_Toggled;
                    
                    // Stop and restart the proxy with the new setting
                    await StopProxy();
                    await StartProxy();
                    
                    // Re-enable the toggle
                    ProxyToggle.Toggled += ProxyToggle_Toggled;
                }
                
                // Save the setting (would be implemented in a full settings system)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling log visibility: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Settings Error", 
                    $"Failed to change log visibility: {ex.Message}"
                );
                
                // Reset the toggle without triggering the event
                ShowLogsToggle.Toggled -= ShowLogsToggle_Toggled;
                ShowLogsToggle.IsOn = !ShowLogsToggle.IsOn;
                _showMitmproxyLogs = ShowLogsToggle.IsOn;
                ShowLogsToggle.Toggled += ShowLogsToggle_Toggled;
            }
        }
        
        private async Task StartProxy()
        {
            try
            {
                // Use the ProxyService to start the proxy
                await ProxyService.StartProxy(_showMitmproxyLogs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting proxy: {ex.Message}");
                throw new Exception($"Failed to start proxy: {ex.Message}", ex);
            }
        }
        
        private async Task StopProxy()
        {
            try
            {
                // Use the ProxyService to stop the proxy
                await ProxyService.StopProxy();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping proxy: {ex.Message}");
                throw new Exception($"Failed to stop proxy: {ex.Message}", ex);
            }
        }
        
        private async void AutoStartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await DialogService.ShowDialog(
                    this.XamlRoot,
                    "Configure Auto-Start",
                    "Do you want to add the MITM core to startup items?",
                    "Add to Startup",
                    "Remove from Startup",
                    "Cancel"
                );
                
                if (result == ContentDialogResult.Primary)
                {
                    // Add to startup
                    await AddToStartup();
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // Remove from startup
                    await RemoveFromStartup();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error configuring auto-start: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Auto-Start Error", 
                    $"Failed to configure auto-start: {ex.Message}"
                );
            }
        }

        private async Task AddToStartup()
        {
            try
            {
                bool success = await ProxyService.AddToStartup(_showMitmproxyLogs);
                if (success)
                {
                    await DialogService.ShowInfoDialog(
                        this.XamlRoot,
                        "Auto-Start Configured", 
                        "MITM Modular has been added to startup items."
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding to startup: {ex.Message}");
                throw new Exception($"Failed to add to startup: {ex.Message}", ex);
            }
        }

        private async Task RemoveFromStartup()
        {
            try
            {
                bool success = await ProxyService.RemoveFromStartup();
                if (success)
                {
                    await DialogService.ShowInfoDialog(
                        this.XamlRoot,
                        "Auto-Start Removed", 
                        "MITM Modular has been removed from startup items."
                    );
                }
                else
                {
                    await DialogService.ShowInfoDialog(
                        this.XamlRoot,
                        "Not Found", 
                        "MITM Modular was not found in startup items."
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing from startup: {ex.Message}");
                throw new Exception($"Failed to remove from startup: {ex.Message}", ex);
            }
        }
        
        private async void ExportTargetsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Placeholder for future implementation
                await DialogService.ShowInfoDialog(
                    this.XamlRoot,
                    "Export Feature", 
                    "The export feature will be implemented in a future version."
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in export function: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Export Error", 
                    $"An error occurred: {ex.Message}"
                );
            }
        }
        
        private async void DeleteAllTargetsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                bool confirmed = await DialogService.ShowConfirmationDialog(
                    this.XamlRoot,
                    "Confirm Delete",
                    "Are you sure you want to delete ALL targets? This action cannot be undone.",
                    "Delete All",
                    "Cancel"
                );
                
                if (!confirmed)
                {
                    return;
                }
                
                string pythonExePath = PythonService.GetPythonExecutablePath();
                
                // Run the CLI command to delete all targets using our embedded Python
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExePath,
                    Arguments = $"-m mitm_modular.cli delete-all",
                    WorkingDirectory = Path.Combine(PythonService.GetAppFolder(), "tools"),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    Debug.WriteLine($"Delete all output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.WriteLine($"Delete all error: {error}");
                    
                    if (process.ExitCode == 0)
                    {
                        await DialogService.ShowInfoDialog(
                            this.XamlRoot,
                            "Targets Deleted", 
                            "All targets have been deleted."
                        );
                    }
                    else
                    {
                        throw new Exception($"Failed to delete all targets. Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting all targets: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Delete Error", 
                    $"Failed to delete all targets: {ex.Message}"
                );
            }
        }
        
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set the button to loading state
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = "Checking...";
                
                // Simulate checking for updates
                await Task.Delay(1500);
                
                // For now, show a dialog stating this is a demo
                await DialogService.ShowInfoDialog(
                    this.XamlRoot,
                    "Update Check", 
                    "This is a demo. Update checking functionality will be implemented in a future version."
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Update Error", 
                    $"Failed to check for updates: {ex.Message}"
                );
            }
            finally
            {
                // Reset the button
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check Now";
            }
        }
        
        private async void InstallCertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set the button to loading state
                InstallCertButton.IsEnabled = false;
                InstallCertButton.Content = "Installing...";
                
                // Find the certificate path in the user's .mitmproxy folder
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string mitmproxyDir = Path.Combine(userProfile, ".mitmproxy");
                string certPath = Path.Combine(mitmproxyDir, "mitmproxy-ca-cert.cer");
                
                // Check if certificate exists
                if (!File.Exists(certPath))
                {
                    // Certificate not found, show error
                    await DialogService.ShowErrorDialog(
                        this.XamlRoot,
                        "Certificate Not Found", 
                        $"Certificate file not found at {certPath}. Please run mitmproxy or mitmdump first to generate the certificate."
                    );
                    return;
                }
                
                // Create and run the certutil command
                var startInfo = new ProcessStartInfo
                {
                    FileName = "certutil",
                    Arguments = $"-addstore root \"{certPath}\"",
                    UseShellExecute = true,  // Show UAC prompt
                    Verb = "runas",          // Request elevation
                    CreateNoWindow = false   // Show the console window for user confirmation
                };
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        await DialogService.ShowInfoDialog(
                            this.XamlRoot,
                            "Certificate Installed", 
                            "The mitmproxy certificate has been successfully installed. " +
                            "HTTPS interception should now work properly."
                        );
                    }
                    else
                    {
                        await DialogService.ShowErrorDialog(
                            this.XamlRoot,
                            "Installation Failed", 
                            "Failed to install the certificate. Please try running the application as administrator."
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing certificate: {ex.Message}");
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Certificate Error", 
                    $"Failed to install certificate: {ex.Message}"
                );
            }
            finally
            {
                // Reset the button
                InstallCertButton.IsEnabled = true;
                InstallCertButton.Content = "Install Certificate";
            }
        }

        // Helper method to retry dependency installation
        private async Task RetryDependencyInstallation()
        {
            try
            {
                // Show progress indicator
                ProgressRing.IsActive = true;
                ProgressContainer.Visibility = Visibility.Visible;
                StatusText.Text = "Retrying dependency installation...";
                
                // Install dependencies - now returns success status and logs
                var installResult = await DependencyService.InstallDependencies();
                
                // Hide progress indicator
                ProgressRing.IsActive = false;
                ProgressContainer.Visibility = Visibility.Collapsed;
                
                if (installResult.Success)
                {
                    await DialogService.ShowInfoDialog(
                        this.XamlRoot,
                        "Installation Complete",
                        "Required dependencies have been installed successfully."
                    );
                }
                else
                {
                    // Show an error dialog with the option to view detailed logs
                    var result = await DialogService.ShowDialog(
                        this.XamlRoot,
                        "Installation Failed",
                        "Failed to install dependencies. You can view the installation logs for more details.",
                        "View Logs",
                        "Try Again",
                        "Close"
                    );
                    
                    // Handle user's choice
                    if (result == ContentDialogResult.Primary)
                    {
                        // User wants to view logs
                        await DialogService.ShowLogDialog(
                            this.XamlRoot,
                            "Installation Logs",
                            installResult.Logs
                        );
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // User wants to try again
                        await RetryDependencyInstallation();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrying dependency installation: {ex.Message}");
                
                // Hide progress indicator
                ProgressRing.IsActive = false;
                ProgressContainer.Visibility = Visibility.Collapsed;
                
                await DialogService.ShowErrorDialog(
                    this.XamlRoot,
                    "Error", 
                    $"An error occurred during installation: {ex.Message}"
                );
            }
        }
    }
} 