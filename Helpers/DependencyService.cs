using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace WinUI_V3.Helpers
{
    public static class DependencyService
    {
        // Check if all required dependencies are available
        public static async Task<bool> CheckDependencies()
        {
            bool pythonAvailable = await PythonService.IsPythonAvailable();
            bool mitmproxyAvailable = await PythonService.IsMitmproxyAvailable();
            
            return pythonAvailable && mitmproxyAvailable;
        }
        
        // Check if Python is installed, and install it if not
        public static async Task<bool> EnsurePythonInstalled()
        {
            try
            {
                // First check if Python is already available
                if (await PythonService.IsPythonAvailable())
                {
                    Debug.WriteLine("Python is already installed");
                    return true;
                }
                
                // Python isn't available, so we'll need to install an embedded version
                Debug.WriteLine("Python is not installed, attempting to install embedded version");
                
                // Path to install embedded Python
                string appFolder = PythonService.GetAppFolder();
                string toolsFolder = Path.Combine(appFolder, "tools");
                string pythonFolder = Path.Combine(toolsFolder, "python");
                
                // Create directories if they don't exist
                if (!Directory.Exists(toolsFolder))
                    Directory.CreateDirectory(toolsFolder);
                    
                if (!Directory.Exists(pythonFolder))
                    Directory.CreateDirectory(pythonFolder);
                
                // Download embedded Python
                string pythonVersion = "3.11.5"; // Choose a stable Python version
                string pythonUrl = $"https://www.python.org/ftp/python/{pythonVersion}/python-{pythonVersion}-embed-amd64.zip";
                string downloadPath = Path.Combine(toolsFolder, "python-embed.zip");
                
                using (var httpClient = new HttpClient())
                {
                    Debug.WriteLine($"Downloading Python from {pythonUrl}");
                    var response = await httpClient.GetAsync(pythonUrl);
                    response.EnsureSuccessStatusCode();
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
                
                // Extract the Python ZIP file
                Debug.WriteLine($"Extracting Python to {pythonFolder}");
                ZipFile.ExtractToDirectory(downloadPath, pythonFolder, true);
                
                // Clean up the ZIP file
                File.Delete(downloadPath);
                
                // Ensure Python is now available
                return await PythonService.IsPythonAvailable();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring Python is installed: {ex.Message}");
                return false;
            }
        }
        
        // Check if mitmproxy is installed, and install it if not
        public static async Task<bool> EnsureMitmproxyInstalled()
        {
            try
            {
                // First check if mitmproxy is already available
                if (await PythonService.IsMitmproxyAvailable())
                {
                    Debug.WriteLine("mitmproxy is already installed");
                    return true;
                }
                
                // Ensure Python is available before attempting to install mitmproxy
                if (!await PythonService.IsPythonAvailable())
                {
                    Debug.WriteLine("Python is not available, cannot install mitmproxy");
                    return false;
                }
                
                // Python is available, so we'll install mitmproxy using pip
                Debug.WriteLine("mitmproxy is not installed, attempting to install");
                
                string pythonPath = PythonService.GetPythonExecutablePath();
                
                // Check if pip is available by running 'python -m pip --version'
                var pipCheckProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-m pip --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                pipCheckProcess.Start();
                await pipCheckProcess.WaitForExitAsync();
                
                bool pipAvailable = pipCheckProcess.ExitCode == 0;
                
                // If pip is not available, we need to install it
                if (!pipAvailable)
                {
                    Debug.WriteLine("pip is not available, installing pip");
                    
                    // Download get-pip.py
                    string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
                    string getPipPath = Path.Combine(Path.GetTempPath(), "get-pip.py");
                    
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync(getPipUrl);
                        response.EnsureSuccessStatusCode();
                        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(getPipPath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }
                    
                    // Install pip
                    var pipInstallProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pythonPath,
                            Arguments = getPipPath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    pipInstallProcess.Start();
                    await pipInstallProcess.WaitForExitAsync();
                    
                    // Clean up get-pip.py
                    File.Delete(getPipPath);
                    
                    // Check if pip was installed successfully
                    if (pipInstallProcess.ExitCode != 0)
                    {
                        Debug.WriteLine("Failed to install pip");
                        return false;
                    }
                }
                
                // Now install mitmproxy using pip
                Debug.WriteLine("Installing mitmproxy using pip");
                
                var mitmproxyInstallProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-m pip install mitmproxy",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                mitmproxyInstallProcess.Start();
                await mitmproxyInstallProcess.WaitForExitAsync();
                
                // Check if mitmproxy was installed successfully
                return await PythonService.IsMitmproxyAvailable();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring mitmproxy is installed: {ex.Message}");
                return false;
            }
        }
        
        // Install all required dependencies
        public static async Task<bool> InstallDependencies()
        {
            try
            {
                // Ensure Python is installed
                if (!await EnsurePythonInstalled())
                {
                    Debug.WriteLine("Failed to install Python");
                    return false;
                }
                
                // Ensure mitmproxy is installed
                if (!await EnsureMitmproxyInstalled())
                {
                    Debug.WriteLine("Failed to install mitmproxy");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing dependencies: {ex.Message}");
                return false;
            }
        }
    }
} 