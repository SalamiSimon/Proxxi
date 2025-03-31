using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Management;

namespace WinUI_V3.Helpers
{
    public static class ProxyService
    {
        // The single proxy process reference
        private static Process? _proxyProcess = null;
        
        // Check if proxy is running
        public static async Task<bool> IsProxyRunning()
        {
            try
            {
                // First check for actual mitmdump processes - this is the most direct check
                var mitmdumpProcesses = Process.GetProcessesByName("mitmdump");
                if (mitmdumpProcesses.Length > 0)
                {
                    // Just having a mitmdump process is enough evidence it's running
                    foreach (var process in mitmdumpProcesses)
                    {
                        try { process.Dispose(); } catch { }
                    }
                    return true;
                }
                
                // Check if the proxy process reference is still valid
                if (_proxyProcess != null && !_proxyProcess.HasExited)
                {
                    return true;
                }
                
                // As a fallback, look for Python processes that might be running our mitmdump
                var pythonProcesses = Process.GetProcessesByName("python");
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
        
        // Helper method to check if a process is our mitmdump process
        private static bool IsMitmdumpProcess(Process process)
        {
            try
            {
                // Check if the process name is mitmdump, that's already enough
                if (process.ProcessName.Equals("mitmdump", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // For Python processes, check the command line if possible
                if (process.ProcessName.Equals("python", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string? commandLine = GetCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(commandLine) && 
                            (commandLine.Contains("mitmdump") || commandLine.Contains("mitm_modular")))
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking command line: {ex.Message}");
                    }

                    // Also check the main module as a fallback
                    if (process.MainModule?.FileName != null)
                    {
                        string processPath = process.MainModule.FileName;
                        if (processPath.Contains("mitmdump"))
                        {
                            return true;
                        }
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
        
        // Helper method to get command line for a process
        private static string? GetCommandLine(int processId)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["CommandLine"]?.ToString();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting command line: {ex.Message}");
                return null;
            }
        }
        
        // Start the proxy server
        public static async Task<bool> StartProxy(bool showLogs = false)
        {
            try
            {
                // Check if proxy is already running
                if (await IsProxyRunning())
                {
                    return true;
                }
                
                string toolsPath = Path.Combine(PythonService.GetAppFolder(), "tools");
                
                // Full path to the run_mitm.py file
                string mitm_core_path = Path.Combine(toolsPath, "run_mitm.py");
                string mitmdumpPath = PythonService.GetMitmdumpPath();
                
                // Verify paths and log them for debugging
                Debug.WriteLine($"Using mitm_core_path: {mitm_core_path}");
                Debug.WriteLine($"Using mitmdumpPath: {mitmdumpPath}");
                
                // Verify mitmdump.exe exists or is in PATH
                if (mitmdumpPath != "mitmdump" && !File.Exists(mitmdumpPath))
                {
                    throw new FileNotFoundException($"Mitmdump not found at: {mitmdumpPath}");
                }
                
                // Start the proxy process using the appropriate mitmdump
                var startInfo = new ProcessStartInfo
                {
                    FileName = mitmdumpPath,
                    Arguments = $"-s \"{mitm_core_path}\" --set block_global=false --listen-port 45871",
                    WorkingDirectory = Path.GetDirectoryName(toolsPath),
                    UseShellExecute = showLogs, // Use shell execute when showing logs
                    RedirectStandardOutput = !showLogs,
                    RedirectStandardError = !showLogs,
                    CreateNoWindow = !showLogs  // Show window based on showLogs setting
                };
                
                _proxyProcess = new Process { StartInfo = startInfo };
                _proxyProcess.Start();
                
                // Wait longer to make sure it starts correctly
                await Task.Delay(5000);
                
                // Try multiple times to check if it's running
                for (int i = 0; i < 3; i++) 
                {
                    if (await IsProxyRunning())
                    {
                        return true; // Success
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
        
        // Stop the proxy server
        public static async Task<bool> StopProxy()
        {
            try
            {
                // Check if proxy is already stopped
                if (!await IsProxyRunning())
                {
                    return true;
                }
                
                // Find and kill all mitmdump processes
                var mitmdumpProcesses = Process.GetProcessesByName("mitmdump");
                
                foreach (var process in mitmdumpProcesses)
                {
                    try
                    {
                        if (IsMitmdumpProcess(process))
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
                
                // Find and kill Python processes running our script
                var pythonProcesses = Process.GetProcessesByName("python");
                foreach (var process in pythonProcesses)
                {
                    try 
                    {
                        if (IsMitmdumpProcess(process))
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error killing Python process: {ex.Message}");
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
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping proxy: {ex.Message}");
                throw new Exception($"Failed to stop proxy: {ex.Message}", ex);
            }
        }
        
        // Create batch file for auto-start
        public static async Task<bool> AddToStartup(bool showLogs = false)
        {
            try
            {
                string toolsPath = Path.Combine(PythonService.GetAppFolder(), "tools");
                string mitm_core_path = Path.Combine(toolsPath, "run_mitm.py");
                string mitmdumpPath = PythonService.GetMitmdumpPath();
                
                if (mitmdumpPath != "mitmdump" && !File.Exists(mitmdumpPath))
                {
                    throw new Exception($"Mitmdump not found at: {mitmdumpPath}");
                }
                
                string startupCommand = $"\"{mitmdumpPath}\" -s \"{mitm_core_path}\" --set block_global=false --listen-port 45871";
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");

                string batchContent;

                if (showLogs)
                {
                    // Create batch file that shows the console window
                    batchContent = $"@echo off\n"
                        + $"cd /d \"{Path.GetDirectoryName(toolsPath)}\"\n"
                        + $"start \"\" {startupCommand}\n"
                        + "exit";
                }
                else
                {
                    // Create batch file that fully hides the console window
                    batchContent = $"@echo off\n"
                        + $"cd /d \"{Path.GetDirectoryName(toolsPath)}\"\n"
                        + $"powershell -Command \"Start-Process '{mitmdumpPath}' -ArgumentList '-s \\\"{mitm_core_path}\\\" --set block_global=false --listen-port 45871' -WindowStyle Hidden -NoNewWindow -PassThru\"\n"
                        + "exit";
                }

                await File.WriteAllTextAsync(startupPath, batchContent);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding to startup: {ex.Message}");
                throw new Exception($"Failed to add to startup: {ex.Message}", ex);
            }
        }
        
        // Remove auto-start
        public static async Task<bool> RemoveFromStartup()
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
                    return true;
                }
                
                return false; // File didn't exist
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing from startup: {ex.Message}");
                throw new Exception($"Failed to remove from startup: {ex.Message}", ex);
            }
        }
        
        // Check if startup is enabled
        public static bool IsStartupEnabled()
        {
            try
            {
                string startupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "MitmModular.bat");
                
                return File.Exists(startupPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking startup status: {ex.Message}");
                return false;
            }
        }

        // Check and update proxy status on application startup
        public static async Task InitializeOnStartup()
        {
            try
            {
                Debug.WriteLine("ProxyService: Checking proxy status on startup...");
                
                // Check for mitmdump processes specifically
                var mitmdumpProcesses = Process.GetProcessesByName("mitmdump");
                if (mitmdumpProcesses.Length > 0)
                {
                    Debug.WriteLine($"ProxyService: Found {mitmdumpProcesses.Length} mitmdump processes running");
                    foreach (var process in mitmdumpProcesses)
                    {
                        try 
                        {
                            Debug.WriteLine($"ProxyService: mitmdump process ID: {process.Id}");
                            try 
                            {
                                var cmdLine = GetCommandLine(process.Id);
                                Debug.WriteLine($"ProxyService: mitmdump command line: {cmdLine ?? "unknown"}");
                            }
                            catch { }
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("ProxyService: No mitmdump processes found by name");
                }
                
                // Check if the proxy is running
                bool isRunning = await IsProxyRunning();
                Debug.WriteLine($"ProxyService: Proxy status on startup: {(isRunning ? "Running" : "Not running")}");
                
                // We don't need to start it here, just detect the state
                // This information will be used to update UI elements
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing proxy service on startup: {ex.Message}");
            }
        }
    }
} 