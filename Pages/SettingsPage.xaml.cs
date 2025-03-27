using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinUI_V3.Helpers;

namespace WinUI_V3.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // HARDCODED PATHS - TEMPORARY
        private readonly string DbPath = @"C:\Users\Sten\Desktop\PROXIMITM\targets.db";
        private readonly string ModularPath = @"C:\Users\Sten\Desktop\PROXIMITM\mitm_modular";
        
        // Process for the proxy server
        private Process? _proxyProcess = null;
        
        public SettingsPage()
        {
            this.InitializeComponent();
            
            // Check if proxy is running and update the toggle
            CheckProxyStatus();
        }
        
        private async void CheckProxyStatus()
        {
            try
            {
                // Check if the proxy process is running
                bool isRunning = await IsProxyRunning();
                
                // Update the toggle without triggering the event
                ProxyToggle.Toggled -= ProxyToggle_Toggled;
                ProxyToggle.IsOn = isRunning;
                ProxyToggle.Toggled += ProxyToggle_Toggled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking proxy status: {ex.Message}");
            }
        }
        
        private async Task<bool> IsProxyRunning()
        {
            try
            {
                // Use a simple command to check if the proxy is running
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m mitm_modular.cli status",
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
                    await process.WaitForExitAsync();
                    
                    // Check if the output indicates the proxy is running
                    return output.Contains("running") && !output.Contains("not running");
                }
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
        
        private async Task StartProxy()
        {
            try
            {
                // Check if proxy is already running
                if (await IsProxyRunning())
                {
                    return;
                }
                
                // Start the proxy process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m mitm_modular.cli start",
                    WorkingDirectory = Path.GetDirectoryName(ModularPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                _proxyProcess = new Process { StartInfo = startInfo };
                _proxyProcess.Start();
                
                // Wait a bit to make sure it starts correctly
                await Task.Delay(1000);
                
                // Check if it's running
                if (!await IsProxyRunning())
                {
                    throw new Exception("Proxy failed to start");
                }
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
                
                // Stop the proxy process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m mitm_modular.cli stop",
                    WorkingDirectory = Path.GetDirectoryName(ModularPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                }
                
                // Cleanup the process reference
                if (_proxyProcess != null)
                {
                    _proxyProcess.Dispose();
                    _proxyProcess = null;
                }
                
                // Wait a bit to make sure it stops correctly
                await Task.Delay(1000);
                
                // Check if it's still running
                if (await IsProxyRunning())
                {
                    throw new Exception("Proxy failed to stop");
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
                string startupCommand = $"python -m mitm_modular.cli start";
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");
                
                string batchContent = $@"@echo off
cd /d ""{Path.GetDirectoryName(ModularPath)}""
{startupCommand}
exit";
                
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
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        await ShowInfoDialog("Targets Deleted", "All targets have been deleted.");
                    }
                    else
                    {
                        throw new Exception("Failed to delete all targets");
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