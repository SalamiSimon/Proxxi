using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WinUI_V3.Helpers;

namespace WinUI_V3.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // HARDCODED PATHS - TEMPORARY
        private readonly string ModularPath = @"C:\Users\Sten\Desktop\PROXIMITM\mitm_modular";
        private readonly string RootModularPath = @"C:\Users\Sten\Desktop\PROXIMITM";
        
        // Process for the proxy server
        private Process? _proxyProcess = null;
        
        // Settings
        private bool _showMitmproxyLogs = false;
        
        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }
        
        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize proxy toggle state based on whether proxy is running
                ProxyToggle.Toggled -= ProxyToggle_Toggled; // Prevent event firing during init
                ProxyToggle.IsOn = await IsProxyRunning();
                ProxyToggle.Toggled += ProxyToggle_Toggled;
                
                // Load the ShowLogs setting value (could be from settings storage in the future)
                _showMitmproxyLogs = false; // default to false
                ShowLogsToggle.IsOn = _showMitmproxyLogs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SettingsPage_Loaded: {ex.Message}");
            }
        }
        
        private async Task<bool> IsProxyRunning()
        {
            try
            {
                // Check if the proxy is running by looking for mitmdump processes
                var processes = Process.GetProcessesByName("mitmdump");
                
                if (processes.Length > 0)
                {
                    // Found mitmdump processes - consider the proxy running
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if proxy is running: {ex.Message}");
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
                await ShowErrorDialog("Proxy Error", $"Failed to {(ProxyToggle.IsOn ? "start" : "stop")} the proxy: {ex.Message}");
                
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
                await ShowErrorDialog("Settings Error", $"Failed to change log visibility: {ex.Message}");
                
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
                // Check if proxy is already running
                if (await IsProxyRunning())
                {
                    return;
                }
                
                // Full path to the mitm_core.py file
                string mitm_core_path = Path.Combine(RootModularPath, "run_mitm.py");
                
                // Start the proxy process - directly using mitmdump
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mitmdump",
                    Arguments = $"-s \"{mitm_core_path}\" --set block_global=false",
                    WorkingDirectory = Path.GetDirectoryName(RootModularPath),
                    UseShellExecute = _showMitmproxyLogs, // Use shell execute when showing logs
                    RedirectStandardOutput = !_showMitmproxyLogs,
                    RedirectStandardError = !_showMitmproxyLogs,
                    CreateNoWindow = !_showMitmproxyLogs  // Show window based on toggle setting
                };
                
                _proxyProcess = new Process { StartInfo = startInfo };
                _proxyProcess.Start();
                
                // Wait longer to make sure it starts correctly - increase from 2s to 5s
                await Task.Delay(5000);
                
                // Try multiple times to check if it's running
                for (int i = 0; i < 3; i++) 
                {
                    if (await IsProxyRunning())
                    {
                        return; // Success
                    }
                    await Task.Delay(1000); // Wait an additional second between retries
                }
                
                throw new Exception("Proxy failed to start after multiple attempts");
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
                // Check if proxy is already stopped
                if (!await IsProxyRunning())
                {
                    return;
                }
                
                // Find and kill all mitmdump processes
                var mitmdumpProcesses = Process.GetProcessesByName("mitmdump");
                
                foreach (var process in mitmdumpProcesses)
                {
                    try
                    {
                        var processModule = process.MainModule?.FileName;
                        if (processModule != null && processModule.Contains("mitmdump"))
                        {
                            // Kill the process
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error killing process: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Cleanup the process reference
                if (_proxyProcess != null)
                {
                    try
                    {
                        if (!_proxyProcess.HasExited)
                        {
                            _proxyProcess.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error killing proxy process: {ex.Message}");
                    }
                    finally
                    {
                        _proxyProcess.Dispose();
                        _proxyProcess = null;
                    }
                }
                
                // Wait a bit to make sure it stops correctly
                await Task.Delay(1000);
                
                // Check if it's still running
                if (await IsProxyRunning())
                {
                    throw new Exception("Proxy failed to stop. Some processes may need to be terminated manually.");
                }
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
                // Create a dialog to configure auto-start
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Configure Auto-Start",
                    Content = "Do you want to add the MITM core to startup items?",
                    PrimaryButtonText = "Add to Startup",
                    SecondaryButtonText = "Remove from Startup",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };
                
                var result = await dialog.ShowAsync();
                
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
                await ShowErrorDialog("Auto-Start Error", $"Failed to configure auto-start: {ex.Message}");
            }
        }
        
        private async Task AddToStartup()
        {
            try
            {
                // Create a batch file or shortcut in the Windows startup folder
                string mitm_core_path = Path.Combine(RootModularPath, "run_mitm.py");
                string startupCommand = $"mitmdump -s \"{mitm_core_path}\" --set block_global=false";
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");
                
                string batchContent;
                
                if (_showMitmproxyLogs)
                {
                    // Create batch file that shows the console window
                    batchContent = $@"@echo off
cd /d ""{Path.GetDirectoryName(RootModularPath)}""
start """" {startupCommand}
exit";
                }
                else
                {
                    // Create batch file that hides the console window
                    batchContent = $@"@echo off
cd /d ""{Path.GetDirectoryName(RootModularPath)}""
start /min """" {startupCommand}
ping 127.0.0.1 -n 1 > nul
:: The following command hides the command window
powershell -WindowStyle Hidden -Command ""Start-Process '{startupCommand}' -WindowStyle Hidden""
exit";
                }
                
                await File.WriteAllTextAsync(startupPath, batchContent);
                
                await ShowInfoDialog("Auto-Start Configured", "MITM Modular has been added to startup items.");
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
                // Remove the batch file or shortcut from the Windows startup folder
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");
                
                if (File.Exists(startupPath))
                {
                    File.Delete(startupPath);
                    await ShowInfoDialog("Auto-Start Removed", "MITM Modular has been removed from startup items.");
                }
                else
                {
                    await ShowInfoDialog("Not Found", "MITM Modular was not found in startup items.");
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
                await ShowInfoDialog("Export Feature", "The export feature will be implemented in a future version.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in export function: {ex.Message}");
                await ShowErrorDialog("Export Error", $"An error occurred: {ex.Message}");
            }
        }
        
        private async void DeleteAllTargetsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Confirm Delete",
                    Content = "Are you sure you want to delete ALL targets? This action cannot be undone.",
                    PrimaryButtonText = "Delete All",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                
                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }
                
                // Run the CLI command to delete all targets
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m mitm_modular.cli delete-all",
                    WorkingDirectory = Path.GetDirectoryName(ModularPath),
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
                        await ShowInfoDialog("Targets Deleted", "All targets have been deleted.");
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
                await ShowErrorDialog("Delete Error", $"Failed to delete all targets: {ex.Message}");
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
                await ShowInfoDialog("Update Check", "This is a demo. Update checking functionality will be implemented in a future version.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                await ShowErrorDialog("Update Error", $"Failed to check for updates: {ex.Message}");
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
                    await ShowErrorDialog("Certificate Not Found", 
                        $"Certificate file not found at {certPath}. Please run mitmproxy or mitmdump first to generate the certificate.");
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
                        await ShowInfoDialog("Certificate Installed", 
                            "The mitmproxy certificate has been successfully installed. " +
                            "HTTPS interception should now work properly.");
                    }
                    else
                    {
                        await ShowErrorDialog("Installation Failed", 
                            "Failed to install the certificate. Please try running the application as administrator.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing certificate: {ex.Message}");
                await ShowErrorDialog("Certificate Error", $"Failed to install certificate: {ex.Message}");
            }
            finally
            {
                // Reset the button
                InstallCertButton.IsEnabled = true;
                InstallCertButton.Content = "Install Certificate";
            }
        }
        
        private async Task ShowErrorDialog(string title, string message)
        {
            try
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                
                await errorDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
            }
        }
        
        private async Task ShowInfoDialog(string title, string message)
        {
            try
            {
                ContentDialog infoDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                
                await infoDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show info dialog: {ex.Message}");
            }
        }
    }
} 