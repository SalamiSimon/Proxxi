using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WinUI_V3.Helpers
{
    public static class PythonService
    {
        // Get application folder (same implementation from Pages)
        public static string GetAppFolder()
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

        // Get Python executable path - finds embedded or system Python
        public static string GetPythonExecutablePath()
        {
            try
            {
                // First, check if the embedded Python exists
                string appDirectory = GetAppFolder();
                string toolsDirectory = Path.Combine(appDirectory, "tools");
                string embeddedPythonPath = Path.Combine(toolsDirectory, "python", "python.exe");
                string embeddedPythonScriptsPath = Path.Combine(toolsDirectory, "python", "Scripts", "python.exe");
                
                // Try multiple locations for the embedded Python
                string[] possiblePaths =
                [
                    embeddedPythonPath,
                    embeddedPythonScriptsPath,
                    Path.Combine(toolsDirectory, "python", "python.exe"),
                    Path.Combine(Directory.GetCurrentDirectory(), "tools", "python", "python.exe"),
                    Path.Combine(Path.GetDirectoryName(toolsDirectory) ?? string.Empty, "python", "python.exe"),
                    "python" // Fallback to system Python only as last resort
                ];
                
                foreach (string path in possiblePaths)
                {
                    Debug.WriteLine($"Checking for Python at: {path}");
                    if (path == "python" || File.Exists(path))
                    {
                        Debug.WriteLine($"Found Python at: {path}");
                        return path;
                    }
                }
                
                // If not found, return the default "python" command which uses the system Python
                Debug.WriteLine("No embedded Python found, falling back to system Python");
                return "python";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Python path: {ex.Message}");
                return "python"; // Fallback to system Python
            }
        }

        // Get mitmdump path
        public static string GetMitmdumpPath()
        {
            try
            {
                string appDirectory = GetAppFolder();
                string toolsDirectory = Path.Combine(appDirectory, "tools");
                
                // Possible locations for mitmdump.exe
                string[] possiblePaths =
                [
                    Path.Combine(toolsDirectory, "python", "Scripts", "mitmdump.exe"),
                    Path.Combine(toolsDirectory, "python", "mitmdump.exe"),
                    Path.Combine(Path.GetDirectoryName(toolsDirectory) ?? string.Empty, "python", "Scripts", "mitmdump.exe"),
                    "mitmdump" // System mitmdump as fallback
                ];
                
                foreach (string path in possiblePaths)
                {
                    Debug.WriteLine($"Checking for mitmdump at: {path}");
                    if (path == "mitmdump" || File.Exists(path))
                    {
                        Debug.WriteLine($"Found mitmdump at: {path}");
                        return path;
                    }
                }
                
                return "mitmdump"; // Fallback to system mitmdump
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting mitmdump path: {ex.Message}");
                return "mitmdump"; // Fallback to system mitmdump
            }
        }

        // Check if Python is installed and available
        public static async Task<bool> IsPythonAvailable()
        {
            try
            {
                string pythonPath = GetPythonExecutablePath();
                
                // If using system Python, need to check if it's in PATH
                if (pythonPath == "python")
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pythonPath,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                
                // Otherwise, check if the file exists
                return File.Exists(pythonPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Python availability: {ex.Message}");
                return false;
            }
        }

        // Check if a Python module is available
        public static async Task<bool> IsModuleAvailable(string moduleName)
        {
            try
            {
                string pythonPath = GetPythonExecutablePath();
                var cmd = $"-c \"import {moduleName}; print('{moduleName} is available')\"";
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = cmd,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking module availability: {ex.Message}");
                return false;
            }
        }

        // Check if mitmproxy is installed
        public static async Task<bool> IsMitmproxyAvailable()
        {
            try
            {
                // First check if the mitmdump module is available
                if (await IsModuleAvailable("mitmproxy"))
                {
                    return true;
                }
                
                // Then check if mitmdump executable is available
                string mitmdumpPath = GetMitmdumpPath();
                
                // If using system mitmdump, check if it's in PATH
                if (mitmdumpPath == "mitmdump")
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = mitmdumpPath,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                
                // Otherwise, check if the file exists
                return File.Exists(mitmdumpPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking mitmproxy availability: {ex.Message}");
                return false;
            }
        }
    }
} 