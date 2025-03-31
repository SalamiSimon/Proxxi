using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI;
using Windows.UI;

namespace WinUI_V3.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // Paths relative to the application directory
        private readonly string ModularPath;
        private readonly string RootModularPath;
        
        // Process for the proxy server
        private Process? _proxyProcess = null;
        
        // Settings
        private bool _showMitmproxyLogs = false;
        
        // Python settings
        private string? _systemPythonPath = null;
        private bool _isPythonReady = false;
        private bool _isMitmproxyInstalled = false;
        
        // Dialog tracking to prevent showing multiple dialogs at once
        private static bool _isDialogOpen = false;
        private static readonly object _dialogLock = new object();
        
        public SettingsPage()
        {
            // Initialize paths relative to the application directory
            string appDirectory = GetAppFolder();
            string toolsPath = Path.Combine(appDirectory, "tools");
            
            RootModularPath = toolsPath;
            ModularPath = Path.Combine(toolsPath, "mitm_modular");
            
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
                // Check for Python only on first launch
                if (IsFirstApplicationLaunch())
                {
                    // Show loading indicator on the Python check button
                    CheckPythonButton.IsEnabled = false;
                    PythonCheckProgressRing.IsActive = true;
                    PythonCheckProgressRing.Visibility = Visibility.Visible;
                    CheckPythonButtonText.Text = "Checking...";
                    
                    try
                    {
                        await CheckPythonEnvironment();
                        // Mark as launched
                        MarkFirstLaunchComplete();
                    }
                    finally
                    {
                        // Reset the button state
                        CheckPythonButton.IsEnabled = true;
                        PythonCheckProgressRing.IsActive = false;
                        PythonCheckProgressRing.Visibility = Visibility.Collapsed;
                        CheckPythonButtonText.Text = "Check Setup";
                    }
                }
                else
                {
                    // Even if we don't do the full check, we still need to check basic Python availability
                    _isPythonReady = await FastPythonCheck();
                    _isMitmproxyInstalled = await FastMitmproxyCheck();
                }
                
                // Initialize proxy toggle state based on whether proxy is running
                ProxyToggle.Toggled -= ProxyToggle_Toggled; // Prevent event firing during init
                ProxyToggle.IsOn = await IsProxyRunning();
                ProxyToggle.Toggled += ProxyToggle_Toggled;
                
                // Load the ShowLogs setting value (could be from settings storage in the future)
                _showMitmproxyLogs = false; // default to false
                ShowLogsToggle.IsOn = _showMitmproxyLogs;
                
                // Update UI based on Python status
                UpdatePythonStatusUI();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SettingsPage_Loaded: {ex.Message}");
            }
        }
        
        private bool IsFirstApplicationLaunch()
        {
            try
            {
                string appDirectory = GetAppFolder();
                string launchFlagPath = Path.Combine(appDirectory, "first_launch.txt");
                
                // If the file doesn't exist, it's the first launch
                if (!File.Exists(launchFlagPath))
                {
                    return true;
                }
                
                // Read the content of the file to check if it's marked as not first launch
                string content = File.ReadAllText(launchFlagPath).Trim();
                return content != "false";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking first launch status: {ex.Message}");
                // In case of error, assume it's not first launch to avoid repeated checks
                return false;
            }
        }
        
        private void MarkFirstLaunchComplete()
        {
            try
            {
                string appDirectory = GetAppFolder();
                string launchFlagPath = Path.Combine(appDirectory, "first_launch.txt");
                
                // Write "false" to indicate it's no longer the first launch
                File.WriteAllText(launchFlagPath, "false");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error marking first launch complete: {ex.Message}");
            }
        }
        
        private void UpdatePythonStatusUI()
        {
            // This method would update any UI elements that depend on Python status
            // For example, disabling the proxy toggle if Python is not available
            ProxyToggle.IsEnabled = _isPythonReady && _isMitmproxyInstalled;
            
            // You could add more UI elements here that show Python status
        }
        
        private async Task CheckPythonEnvironment()
        {
            try
            {
                // Run the Python checker script
                string appDirectory = GetAppFolder();
                string toolsDirectory = Path.Combine(appDirectory, "tools");
                string checkerScript = Path.Combine(toolsDirectory, "check_python.py");
                
                if (!File.Exists(checkerScript))
                {
                    await ShowErrorDialog("Error", "Python environment checker script not found. Please reinstall the application.");
                    return;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{checkerScript}\"",
                    WorkingDirectory = toolsDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                string output = "";
                string error = "";
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    
                    // Set up a timeout task to prevent hanging
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var processExitTask = process.WaitForExitAsync();
                    
                    // Create a timeout task
                    var timeoutTask = Task.Delay(15000); // 15 second timeout
                    
                    // Wait for either the process to complete or the timeout to occur
                    Task completedTask = await Task.WhenAny(processExitTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // Process timed out - try to kill it and show an error
                        Debug.WriteLine("Python checker process timed out");
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                            }
                        }
                        catch (Exception killEx)
                        {
                            Debug.WriteLine($"Error killing Python checker process: {killEx.Message}");
                        }
                        
                        await ShowErrorDialog("Python Check Timeout", 
                            "The Python environment check took too long to complete. This may indicate an issue with your Python installation.");
                        
                        _isPythonReady = false;
                        _isMitmproxyInstalled = false;
                        UpdatePythonStatusUI();
                        return;
                    }
                    
                    // If we get here, the process completed before the timeout
                    output = await outputTask;
                    error = await errorTask;
                    
                    Debug.WriteLine($"Python checker output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        Debug.WriteLine($"Python checker error: {error}");
                    
                    // Parse the output JSON
                    try
                    {
                        Debug.WriteLine($"Raw output from Python checker: {output}");
                        
                        if (string.IsNullOrWhiteSpace(output))
                        {
                            Debug.WriteLine("Python checker returned empty output");
                            _isPythonReady = false;
                            _isMitmproxyInstalled = false;
                            
                            await ShowErrorDialog("Python Setup Error", 
                                "The Python environment checker returned empty output. Please make sure Python is installed and accessible.");
                            return;
                        }
                        
                        // Extract the JSON portion between the markers
                        string jsonText = output;
                        const string startMarker = "###JSON_RESULT_START###";
                        const string endMarker = "###JSON_RESULT_END###";
                        
                        int startIndex = output.IndexOf(startMarker);
                        if (startIndex >= 0)
                        {
                            startIndex += startMarker.Length;
                            int endIndex = output.IndexOf(endMarker, startIndex);
                            
                            if (endIndex > startIndex)
                            {
                                jsonText = output.Substring(startIndex, endIndex - startIndex).Trim();
                                Debug.WriteLine($"Extracted JSON between markers: {jsonText}");
                            }
                            else
                            {
                                Debug.WriteLine("End marker not found in output");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Start marker not found in output");
                        }
                        
                        var result = JsonSerializer.Deserialize<PythonCheckResult>(jsonText);
                        
                        if (result != null)
                        {
                            _isPythonReady = result.python_found;
                            _systemPythonPath = result.python_path;
                            _isMitmproxyInstalled = result.mitmproxy_installed;
                            
                            UpdatePythonStatusUI();
                            
                            if (!_isPythonReady)
                            {
                                // Python not found
                                Debug.WriteLine("Python not found based on check result");
                                await ShowErrorDialog("Python Not Found", 
                                    "No suitable Python installation was found. Please install Python 3.8 or newer and try again.");
                            }
                            else if (!_isMitmproxyInstalled && result.mitmproxy_install_attempted && !result.mitmproxy_install_success)
                            {
                                // Failed to install mitmproxy
                                await ShowErrorDialog("Dependency Installation Failed", 
                                    "Failed to install required dependencies. You may need to run this application as administrator or manually install mitmproxy:\n\n" +
                                    "pip install mitmproxy");
                            }
                            else if (_isMitmproxyInstalled)
                            {
                                // Everything is good - wait a moment to ensure dialog is fully dismissed
                                await Task.Delay(500);
                                await ShowInfoDialog("Setup Complete", 
                                    "Python and all required dependencies are installed and ready to use.");
                            }
                        }
                        else
                        {
                            await ShowErrorDialog("Setup Error", 
                                "Failed to check Python environment. You may need to manually install Python and mitmproxy.");
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON parsing error from Python checker: {jsonEx.Message}");
                        Debug.WriteLine($"Problem JSON output: {output}");
                        
                        await ShowErrorDialog("Python Setup Error", 
                            "The application couldn't properly read information from the Python checker. This may indicate Python is not correctly installed. Please ensure Python 3.8 or newer is installed on your system.");
                        
                        _isPythonReady = false;
                        _isMitmproxyInstalled = false;
                        UpdatePythonStatusUI();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing Python checker output: {ex.Message}");
                        await ShowErrorDialog("Setup Error", 
                            "Failed to check Python environment. You may need to manually install Python and mitmproxy.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Python environment: {ex.Message}");
                await ShowErrorDialog("Setup Error", $"Failed to check Python environment: {ex.Message}");
            }
        }
        
        private class PythonCheckResult
        {
            public bool python_found { get; set; }
            public string? python_path { get; set; }
            public string? python_version { get; set; }
            public bool mitmproxy_installed { get; set; }
            public bool mitmproxy_install_attempted { get; set; }
            public bool mitmproxy_install_success { get; set; }
            public string? error { get; set; }
        }
        
        private async Task<bool> IsProxyRunning()
        {
            try
            {
                // First, check if our process is still alive
                if (_proxyProcess != null && !_proxyProcess.HasExited)
                {
                    Debug.WriteLine("Found our own proxy process still running");
                    return true;
                }
                
                // Check if the port is in use
                bool isPortInUse = await IsPortInUse(45871);
                if (isPortInUse)
                {
                    Debug.WriteLine("Port 45871 is in use - proxy appears to be running");
                    return true;
                }
                
                // Look for proxy processes running with Python
                var pythonProcesses = Process.GetProcessesByName("python");
                foreach (var process in pythonProcesses)
                {
                    try
                    {
                        string commandLine = GetCommandLine(process.Id);
                        if (commandLine != null && 
                            (commandLine.Contains("mitmproxy") || 
                             commandLine.Contains("mitmdump") || 
                             commandLine.Contains("45871")))
                        {
                            Debug.WriteLine($"Found Python process running mitmproxy: {process.Id}");
                            process.Dispose();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking Python process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                // Check processes with Scripts\python.exe name
                var scriptsPythonProcesses = Process.GetProcessesByName("python.exe");
                foreach (var process in scriptsPythonProcesses)
                {
                    try
                    {
                        string commandLine = GetCommandLine(process.Id);
                        if (commandLine != null && 
                            (commandLine.Contains("mitmproxy") || 
                             commandLine.Contains("mitmdump") || 
                             commandLine.Contains("45871")))
                        {
                            Debug.WriteLine($"Found Python.exe process running mitmproxy: {process.Id}");
                            process.Dispose();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking Python.exe process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                Debug.WriteLine("No mitmproxy processes found and port is not in use");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if proxy is running: {ex.Message}");
                return false;
            }
        }
        
        private string GetCommandLine(int processId)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
                using var results = searcher.Get();
                
                foreach (var obj in results)
                {
                    return obj["CommandLine"]?.ToString();
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting command line for process {processId}: {ex.Message}");
                return null;
            }
        }
        
        private async Task<bool> IsPortInUse(int port)
        {
            try
            {
                // Check if the port is in use using netstat
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                // Look for our port in the output
                return output.Contains($":{port} ");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking port: {ex.Message}");
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
                
                // Just update the setting without restarting the proxy
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
                
                // Verify that Python and mitmproxy are available
                if (!_isPythonReady || !_isMitmproxyInstalled)
                {
                    await CheckPythonEnvironment();
                    
                    if (!_isPythonReady || !_isMitmproxyInstalled)
                    {
                        throw new Exception("Python or mitmproxy is not available. Please check the Python installation.");
                    }
                }
                
                // Try to start proxy with system Python
                bool success = await TryStartProxyWithSystemPython();
                
                if (!success)
                {
                    // Show detailed error message
                    await ShowProxyStartupError();
                    throw new Exception("Proxy failed to start. Please ensure mitmproxy is installed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting proxy: {ex.Message}");
                
                // Clean up the process reference if it exists
                if (_proxyProcess != null)
                {
                    try
                    {
                        // Check if process was successfully created and started
                        // HasExited property will throw an exception if process hasn't started
                        try
                        {
                            if (!_proxyProcess.HasExited)
                            {
                                _proxyProcess.Kill();
                            }
                        }
                        catch
                        {
                            // Process was never successfully started
                            Debug.WriteLine("Process was never successfully started");
                        }
                    }
                    catch (Exception killEx)
                    {
                        Debug.WriteLine($"Error killing proxy process: {killEx.Message}");
                    }
                    
                    try
                    {
                        _proxyProcess.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        Debug.WriteLine($"Error disposing proxy process: {disposeEx.Message}");
                    }
                    
                    _proxyProcess = null;
                }
                
                throw new Exception($"Failed to start proxy: {ex.Message}", ex);
            }
        }
        
        private async Task<bool> TryStartProxyWithSystemPython()
        {
            try
            {
                // Check if mitmdump is directly executable
                bool mitmdumpExists = false;
                try
                {
                    var checkInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "mitmdump",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    
                    using var checkProcess = new Process { StartInfo = checkInfo };
                    checkProcess.Start();
                    string output = await checkProcess.StandardOutput.ReadToEndAsync();
                    await checkProcess.WaitForExitAsync();
                    
                    mitmdumpExists = checkProcess.ExitCode == 0 && !string.IsNullOrEmpty(output);
                    Debug.WriteLine($"mitmdump exists in PATH: {mitmdumpExists}");
                    if (mitmdumpExists)
                    {
                        Debug.WriteLine($"mitmdump path: {output.Trim()}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking mitmdump: {ex.Message}");
                }
                
                if (!mitmdumpExists)
                {
                    Debug.WriteLine("mitmdump not found in PATH");
                    return false;
                }
                
                // Full path to the mitm_core.py file
                string appDirectory = GetAppFolder();
                string toolsDirectory = Path.Combine(appDirectory, "tools");
                string mitm_core_path = Path.Combine(toolsDirectory, "run_mitm.py");
                
                // Check if the mitm_core.py file exists
                if (!File.Exists(mitm_core_path))
                {
                    Debug.WriteLine($"MITM core script not found at {mitm_core_path}");
                    return false;
                }
                
                // Start mitmproxy with our script
                Debug.WriteLine("Starting proxy directly...");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mitmdump",
                    Arguments = $"-s \"{mitm_core_path}\" --set block_global=false --listen-port 45871",
                    WorkingDirectory = toolsDirectory,
                    UseShellExecute = _showMitmproxyLogs, // Show window if logs are enabled
                    RedirectStandardOutput = !_showMitmproxyLogs,
                    RedirectStandardError = !_showMitmproxyLogs,
                    CreateNoWindow = !_showMitmproxyLogs,
                    WindowStyle = _showMitmproxyLogs ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
                };
                
                // Add environment variables only if we're not using the shell (UseShellExecute=false)
                // Otherwise it will cause an exception
                if (!_showMitmproxyLogs)
                {
                    // Add some helpful environment variables
                    startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";                   // Force UTF-8 mode
                    startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";         // Use UTF-8 for stdin/stdout
                    startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";             // Disable output buffering
                    startInfo.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "1";     // Use legacy stdio on Windows
                }
                
                Debug.WriteLine($"Starting proxy directly: {startInfo.FileName} {startInfo.Arguments}");
                Debug.WriteLine($"Working directory: {startInfo.WorkingDirectory}");
                
                _proxyProcess = new Process { StartInfo = startInfo };
                
                // If not showing window, capture outputs
                if (!_showMitmproxyLogs)
                {
                    // Always capture output for diagnostics
                    var errorOutput = new System.Text.StringBuilder();
                    var standardOutput = new System.Text.StringBuilder();
                    
                    _proxyProcess.OutputDataReceived += (sender, args) => 
                    {
                        if (args.Data != null)
                        {
                            standardOutput.AppendLine(args.Data);
                            Debug.WriteLine($"Proxy output: {args.Data}");
                        }
                    };
                    
                    _proxyProcess.ErrorDataReceived += (sender, args) => 
                    {
                        if (args.Data != null)
                        {
                            errorOutput.AppendLine(args.Data);
                            Debug.WriteLine($"Proxy error: {args.Data}");
                        }
                    };
                    
                    _proxyProcess.Start();
                    _proxyProcess.BeginOutputReadLine();
                    _proxyProcess.BeginErrorReadLine();
                }
                else
                {
                    _proxyProcess.Start();
                }
                
                // Wait to make sure it starts correctly
                await Task.Delay(5000);
                
                // Try multiple times to check if it's running
                for (int i = 0; i < 3; i++) 
                {
                    if (await IsProxyRunning())
                    {
                        Debug.WriteLine("Proxy started successfully with system Python");
                        return true; // Success
                    }
                    Debug.WriteLine($"Proxy not running after attempt {i+1}, waiting...");
                    await Task.Delay(1000); // Wait an additional second between retries
                }
                
                // If we get here, the proxy failed to start
                Debug.WriteLine("Failed to start proxy with system Python.");
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error using system Python: {ex.Message}");
                
                // Clean up the process reference if it exists
                if (_proxyProcess != null)
                {
                    try
                    {
                        // Check if process was successfully created and started
                        // HasExited property will throw an exception if process hasn't started
                        try
                        {
                            if (!_proxyProcess.HasExited)
                            {
                                _proxyProcess.Kill();
                            }
                        }
                        catch
                        {
                            // Process was never successfully started
                            Debug.WriteLine("Process was never successfully started");
                        }
                    }
                    catch (Exception killEx)
                    {
                        Debug.WriteLine($"Error killing proxy process: {killEx.Message}");
                    }
                    
                    try
                    {
                        _proxyProcess.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        Debug.WriteLine($"Error disposing proxy process: {disposeEx.Message}");
                    }
                    
                    _proxyProcess = null;
                }
                
                return false;
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
                        // Check if process was successfully created and started
                        // HasExited property will throw an exception if process hasn't started
                        try
                        {
                            if (!_proxyProcess.HasExited)
                            {
                                _proxyProcess.Kill();
                            }
                        }
                        catch
                        {
                            // Process was never successfully started
                            Debug.WriteLine("Process was never successfully started");
                        }
                    }
                    catch (Exception killEx)
                    {
                        Debug.WriteLine($"Error killing proxy process: {killEx.Message}");
                    }
                    finally
                    {
                        try
                        {
                            _proxyProcess.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            Debug.WriteLine($"Error disposing proxy process: {disposeEx.Message}");
                        }
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
                // Get the actual path to mitmdump
                string mitmdumpPath = "mitmdump"; // Default fallback
                try
                {
                    var checkInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "mitmdump",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    
                    using var checkProcess = new Process { StartInfo = checkInfo };
                    checkProcess.Start();
                    string output = await checkProcess.StandardOutput.ReadToEndAsync();
                    await checkProcess.WaitForExitAsync();
                    
                    if (checkProcess.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        // Take the first line if multiple paths are returned
                        mitmdumpPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        Debug.WriteLine($"Found mitmdump at: {mitmdumpPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finding mitmdump path: {ex.Message}");
                }
                
                // Get application directory
                string appDirectory = GetAppFolder();
                string toolsDirectory = Path.Combine(appDirectory, "tools");
                string mitm_core_path = Path.Combine(toolsDirectory, "run_mitm.py");
                
                // Create a batch file for startup
                string batchContent;

                if (_showMitmproxyLogs)
                {
                    // Create batch file that shows the console window
                    batchContent = $"@echo off\n"
                        + $"cd /d \"{toolsDirectory}\"\n"
                        + $"set \"PYTHONUTF8=1\"\n"
                        + $"set \"PYTHONIOENCODING=utf-8\"\n"
                        + $"set \"PYTHONUNBUFFERED=1\"\n"
                        + $"set \"PYTHONLEGACYWINDOWSSTDIO=1\"\n"
                        + $"echo Starting mitmproxy with visible console...\n"
                        + $"\"{mitmdumpPath}\" -s \"{mitm_core_path}\" --set block_global=false --listen-port 45871\n"
                        + "pause";
                }
                else
                {
                    // Create batch file that fully hides the console window
                    batchContent = $"@echo off\n"
                        + $"cd /d \"{toolsDirectory}\"\n"
                        + $"set \"PYTHONUTF8=1\"\n"
                        + $"set \"PYTHONIOENCODING=utf-8\"\n"
                        + $"set \"PYTHONUNBUFFERED=1\"\n"
                        + $"set \"PYTHONLEGACYWINDOWSSTDIO=1\"\n"
                        + $"powershell -Command \"$env:PYTHONUTF8='1'; $env:PYTHONIOENCODING='utf-8'; $env:PYTHONUNBUFFERED='1'; $env:PYTHONLEGACYWINDOWSSTDIO='1'; Start-Process '{mitmdumpPath.Replace("\\", "\\\\")}' -ArgumentList '-s \\\"{mitm_core_path.Replace("\\", "\\\\")}\\\" --set block_global=false --listen-port 45871' -WindowStyle Hidden -NoNewWindow -PassThru\"\n";
                }

                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");

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
                
                // Check if Python is available
                if (string.IsNullOrEmpty(_systemPythonPath) || !_isPythonReady)
                {
                    await ShowErrorDialog("Python Not Available", 
                        "Python is not properly configured. Please check your Python installation.");
                    return;
                }
                
                // Run the CLI command to delete all targets
                var startInfo = new ProcessStartInfo
                {
                    FileName = _systemPythonPath,
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
                // Check if a dialog is already open
                lock (_dialogLock)
                {
                    if (_isDialogOpen)
                    {
                        Debug.WriteLine($"Dialog already open, skipping error dialog: {title} - {message}");
                        return;
                    }
                    _isDialogOpen = true;
                }
                
                try
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = title,
                        Content = message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    
                    // Use a timeout to prevent the dialog from hanging indefinitely
                    var dialogTask = errorDialog.ShowAsync();
                    var timeoutTask = Task.Delay(30000); // 30 second timeout
                    
                    // Convert IAsyncOperation to Task
                    Task<ContentDialogResult> wrappedDialogTask = dialogTask.AsTask();
                    
                    var completedTask = await Task.WhenAny(wrappedDialogTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // Dialog timed out - try to hide it
                        try
                        {
                            errorDialog.Hide();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to hide timed out error dialog: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    // Always mark the dialog as closed when we're done
                    lock (_dialogLock)
                    {
                        _isDialogOpen = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
                // Ensure we reset the dialog state
                lock (_dialogLock)
                {
                    _isDialogOpen = false;
                }
            }
        }
        
        private async Task ShowInfoDialog(string title, string message)
        {
            try
            {
                // Check if a dialog is already open
                lock (_dialogLock)
                {
                    if (_isDialogOpen)
                    {
                        Debug.WriteLine($"Dialog already open, skipping info dialog: {title} - {message}");
                        return;
                    }
                    _isDialogOpen = true;
                }
                
                try
                {
                    ContentDialog infoDialog = new ContentDialog
                    {
                        Title = title,
                        Content = message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    
                    // Use a timeout to prevent the dialog from hanging indefinitely
                    var dialogTask = infoDialog.ShowAsync();
                    var timeoutTask = Task.Delay(30000); // 30 second timeout
                    
                    // Convert IAsyncOperation to Task
                    Task<ContentDialogResult> wrappedDialogTask = dialogTask.AsTask();
                    
                    var completedTask = await Task.WhenAny(wrappedDialogTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // Dialog timed out - try to hide it
                        try
                        {
                            infoDialog.Hide();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to hide timed out info dialog: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    // Always mark the dialog as closed when we're done
                    lock (_dialogLock)
                    {
                        _isDialogOpen = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show info dialog: {ex.Message}");
                // Ensure we reset the dialog state
                lock (_dialogLock)
                {
                    _isDialogOpen = false;
                }
            }
        }
        
        private async Task ShowProxyStartupError()
        {
            try
            {
                // Check if Python is properly configured
                if (string.IsNullOrEmpty(_systemPythonPath) || !_isPythonReady)
                {
                    await ShowErrorDialog("Python Not Available", 
                        "Python is not properly configured. Please check your Python installation and try again.");
                    return;
                }
                
                // Create a buffer for diagnostics info
                var diagnosticsOutput = new System.Text.StringBuilder();
                diagnosticsOutput.AppendLine("=== PYTHON DIAGNOSTICS ===");
                
                // Check if mitmproxy is properly installed
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _systemPythonPath,
                        Arguments = "-m mitmproxy.tools.main --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    diagnosticsOutput.AppendLine($"mitmproxy version check exit code: {process.ExitCode}");
                    diagnosticsOutput.AppendLine($"mitmproxy version output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        diagnosticsOutput.AppendLine($"mitmproxy version error: {error}");
                }
                catch (Exception ex)
                {
                    diagnosticsOutput.AppendLine($"Error checking mitmproxy version: {ex.Message}");
                }
                
                // Check the port 
                try
                {
                    bool isPortInUse = await IsPortInUse(45871);
                    diagnosticsOutput.AppendLine($"Port 45871 in use: {isPortInUse}");
                }
                catch (Exception ex)
                {
                    diagnosticsOutput.AppendLine($"Error checking port: {ex.Message}");
                }
                
                Debug.WriteLine(diagnosticsOutput.ToString());
                
                // Show error dialog with detailed information
                await ShowErrorDialog("Proxy Startup Error", 
                    "Failed to start the MITM proxy. Try the following steps to diagnose the issue:\n\n" +
                    "1. Ensure Python is correctly installed and in your PATH\n\n" +
                    "2. Try manually installing mitmproxy: pip install mitmproxy\n\n" +
                    "3. Check if port 45871 is already in use by another application\n\n" +
                    "4. Check Windows Firewall settings to ensure Python/mitmproxy is allowed\n\n" +
                    "The diagnostic information has been logged to the output window.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ShowProxyStartupError: {ex.Message}");
                await ShowErrorDialog("Proxy Error", $"Failed to diagnose proxy startup error: {ex.Message}");
            }
        }

        // Helper method to show any ContentDialog with proper tracking
        private async Task<ContentDialogResult> ShowDialog(ContentDialog dialog)
        {
            ContentDialogResult result = ContentDialogResult.None;
            
            try
            {
                // Check if a dialog is already open
                lock (_dialogLock)
                {
                    if (_isDialogOpen)
                    {
                        Debug.WriteLine($"Dialog already open, skipping dialog: {dialog.Title}");
                        return ContentDialogResult.None;
                    }
                    _isDialogOpen = true;
                }
                
                try
                {
                    // Use a timeout to prevent the dialog from hanging indefinitely
                    var dialogTask = dialog.ShowAsync();
                    var timeoutTask = Task.Delay(30000); // 30 second timeout
                    
                    // Convert IAsyncOperation to Task
                    Task<ContentDialogResult> wrappedDialogTask = dialogTask.AsTask();
                    
                    var completedTask = await Task.WhenAny(wrappedDialogTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // Dialog timed out - try to hide it
                        try
                        {
                            dialog.Hide();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to hide timed out dialog: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Get the result from the dialog
                        result = await wrappedDialogTask;
                    }
                }
                finally
                {
                    // Always mark the dialog as closed when we're done
                    lock (_dialogLock)
                    {
                        _isDialogOpen = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show dialog: {ex.Message}");
                
                // Ensure we reset the dialog state
                lock (_dialogLock)
                {
                    _isDialogOpen = false;
                }
            }
            
            return result;
        }
        
        private async void ResetFirstLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set the button to loading state
                ResetFirstLaunchButton.IsEnabled = false;
                ResetFirstLaunchButton.Content = "Resetting...";
                
                string appDirectory = GetAppFolder();
                string launchFlagPath = Path.Combine(appDirectory, "first_launch.txt");
                
                // Set the flag to true or delete the file to trigger a fresh check
                if (File.Exists(launchFlagPath))
                {
                    File.WriteAllText(launchFlagPath, "true");
                }
                
                await ShowInfoDialog("Reset Complete", 
                    "The first launch flag has been reset. The Python environment check will run the next time you launch the application.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting first launch flag: {ex.Message}");
                await ShowErrorDialog("Reset Error", $"Failed to reset first launch flag: {ex.Message}");
            }
            finally
            {
                // Reset the button
                ResetFirstLaunchButton.IsEnabled = true;
                ResetFirstLaunchButton.Content = "Reset";
            }
        }
        
        private async void CheckPythonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button and show loading indicator
                CheckPythonButton.IsEnabled = false;
                PythonCheckProgressRing.IsActive = true;
                PythonCheckProgressRing.Visibility = Visibility.Visible;
                CheckPythonButtonText.Text = "Checking...";
                
                // Run the Python environment check
                await CheckPythonEnvironment();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Python environment: {ex.Message}");
                await ShowErrorDialog("Check Error", $"Failed to check Python environment: {ex.Message}");
            }
            finally
            {
                // Reset the button state
                CheckPythonButton.IsEnabled = true;
                PythonCheckProgressRing.IsActive = false;
                PythonCheckProgressRing.Visibility = Visibility.Collapsed;
                CheckPythonButtonText.Text = "Check Setup";
            }
        }
        
        private async Task<bool> FastPythonCheck()
        {
            try
            {
                // Perform a simple check to see if Python is available
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();
                
                _systemPythonPath = "python"; // Set default path
                
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<bool> FastMitmproxyCheck()
        {
            try
            {
                // Perform a simple check to see if mitmproxy is installed
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "mitmdump",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0 && !string.IsNullOrEmpty(output);
            }
            catch
            {
                return false;
            }
        }
    }
} 