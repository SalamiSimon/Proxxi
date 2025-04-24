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
        public static async Task<(bool Success, string Logs)> EnsurePythonInstalled()
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== Python Installation Log ===");
            
            try
            {
                // First check if Python is already available
                if (await PythonService.IsPythonAvailable())
                {
                    logs.AppendLine("Python is already installed");
                    Debug.WriteLine("Python is already installed");
                    return (true, logs.ToString());
                }
                
                // Python isn't available, so we'll need to install an embedded version
                logs.AppendLine("Python is not installed, attempting to install embedded version");
                Debug.WriteLine("Python is not installed, attempting to install embedded version");
                
                // Path to install embedded Python
                string appFolder = PythonService.GetAppFolder();
                string toolsFolder = Path.Combine(appFolder, "tools");
                string pythonFolder = Path.Combine(toolsFolder, "python");
                
                logs.AppendLine($"Application folder: {appFolder}");
                logs.AppendLine($"Tools folder: {toolsFolder}");
                logs.AppendLine($"Python folder: {pythonFolder}");
                
                // Create directories if they don't exist
                if (!Directory.Exists(toolsFolder))
                {
                    logs.AppendLine($"Creating tools folder: {toolsFolder}");
                    Directory.CreateDirectory(toolsFolder);
                }
                    
                if (!Directory.Exists(pythonFolder))
                {
                    logs.AppendLine($"Creating Python folder: {pythonFolder}");
                    Directory.CreateDirectory(pythonFolder);
                }
                
                // Download embedded Python
                string pythonVersion = "3.11.5"; // Choose a stable Python version
                string pythonUrl = $"https://www.python.org/ftp/python/{pythonVersion}/python-{pythonVersion}-embed-amd64.zip";
                string downloadPath = Path.Combine(toolsFolder, "python-embed.zip");
                
                logs.AppendLine($"Using Python version: {pythonVersion}");
                logs.AppendLine($"Download URL: {pythonUrl}");
                logs.AppendLine($"Download path: {downloadPath}");
                
                using (var httpClient = new HttpClient())
                {
                    logs.AppendLine($"Downloading Python from {pythonUrl}");
                    Debug.WriteLine($"Downloading Python from {pythonUrl}");
                    
                    var response = await httpClient.GetAsync(pythonUrl);
                    logs.AppendLine($"Download response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                    logs.AppendLine("Python download completed successfully");
                }
                
                // Extract the Python ZIP file
                logs.AppendLine($"Extracting Python to {pythonFolder}");
                Debug.WriteLine($"Extracting Python to {pythonFolder}");
                ZipFile.ExtractToDirectory(downloadPath, pythonFolder, true);
                logs.AppendLine("Python extraction completed");
                
                // Clean up the ZIP file
                logs.AppendLine($"Cleaning up ZIP file: {downloadPath}");
                File.Delete(downloadPath);
                
                // Ensure Python is now available
                bool pythonAvailable = await PythonService.IsPythonAvailable();
                logs.AppendLine($"Python availability check: {(pythonAvailable ? "Available" : "Not available")}");
                return (pythonAvailable, logs.ToString());
            }
            catch (Exception ex)
            {
                logs.AppendLine($"ERROR: {ex.Message}");
                logs.AppendLine(ex.StackTrace);
                Debug.WriteLine($"Error ensuring Python is installed: {ex.Message}");
                return (false, logs.ToString());
            }
        }
        
        // Check if mitmproxy is installed, and install it if not
        public static async Task<(bool Success, string Logs)> EnsureMitmproxyInstalled()
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== Mitmproxy Installation Log ===");
            
            try
            {
                // First check if mitmproxy is already available
                if (await PythonService.IsMitmproxyAvailable())
                {
                    logs.AppendLine("mitmproxy is already installed");
                    Debug.WriteLine("mitmproxy is already installed");
                    return (true, logs.ToString());
                }
                
                // Ensure Python is available before attempting to install mitmproxy
                if (!await PythonService.IsPythonAvailable())
                {
                    logs.AppendLine("ERROR: Python is not available, cannot install mitmproxy");
                    Debug.WriteLine("Python is not available, cannot install mitmproxy");
                    return (false, logs.ToString());
                }
                
                // Python is available, so we'll install mitmproxy using pip
                logs.AppendLine("mitmproxy is not installed, attempting to install");
                Debug.WriteLine("mitmproxy is not installed, attempting to install");
                
                string pythonPath = PythonService.GetPythonExecutablePath();
                logs.AppendLine($"Using Python from: {pythonPath}");
                
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
                
                logs.AppendLine("Checking pip availability");
                pipCheckProcess.Start();
                string pipCheckOutput = await pipCheckProcess.StandardOutput.ReadToEndAsync();
                string pipCheckError = await pipCheckProcess.StandardError.ReadToEndAsync();
                await pipCheckProcess.WaitForExitAsync();
                
                logs.AppendLine($"Pip check exit code: {pipCheckProcess.ExitCode}");
                if (!string.IsNullOrWhiteSpace(pipCheckOutput))
                    logs.AppendLine($"Pip check output: {pipCheckOutput}");
                if (!string.IsNullOrWhiteSpace(pipCheckError))
                    logs.AppendLine($"Pip check error: {pipCheckError}");
                
                bool pipAvailable = pipCheckProcess.ExitCode == 0;
                logs.AppendLine($"Pip available: {pipAvailable}");
                
                // If pip is not available, we need to install it
                if (!pipAvailable)
                {
                    logs.AppendLine("pip is not available, installing pip");
                    Debug.WriteLine("pip is not available, installing pip");
                    
                    // Download get-pip.py
                    string getPipUrl = "https://bootstrap.pypa.io/get-pip.py";
                    string getPipPath = Path.Combine(Path.GetTempPath(), "get-pip.py");
                    
                    logs.AppendLine($"Downloading get-pip.py from {getPipUrl}");
                    logs.AppendLine($"Saving to {getPipPath}");
                    
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync(getPipUrl);
                        logs.AppendLine($"Download response status: {response.StatusCode}");
                        response.EnsureSuccessStatusCode();
                        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(getPipPath, FileMode.Create, FileAccess.Write))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                        logs.AppendLine("get-pip.py download completed");
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
                    
                    logs.AppendLine("Installing pip");
                    pipInstallProcess.Start();
                    string pipInstallOutput = await pipInstallProcess.StandardOutput.ReadToEndAsync();
                    string pipInstallError = await pipInstallProcess.StandardError.ReadToEndAsync();
                    await pipInstallProcess.WaitForExitAsync();
                    
                    logs.AppendLine($"Pip install exit code: {pipInstallProcess.ExitCode}");
                    if (!string.IsNullOrWhiteSpace(pipInstallOutput))
                        logs.AppendLine($"Pip install output: {pipInstallOutput}");
                    if (!string.IsNullOrWhiteSpace(pipInstallError))
                        logs.AppendLine($"Pip install error: {pipInstallError}");
                    
                    // Clean up get-pip.py
                    logs.AppendLine($"Cleaning up {getPipPath}");
                    File.Delete(getPipPath);
                    
                    // Check if pip was installed successfully
                    if (pipInstallProcess.ExitCode != 0)
                    {
                        logs.AppendLine("ERROR: Failed to install pip");
                        Debug.WriteLine("Failed to install pip");
                        return (false, logs.ToString());
                    }
                    
                    logs.AppendLine("Pip installation completed successfully");
                }
                
                // Now install mitmproxy using pip
                logs.AppendLine("Installing mitmproxy using pip");
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
                string mitmInstallOutput = await mitmproxyInstallProcess.StandardOutput.ReadToEndAsync();
                string mitmInstallError = await mitmproxyInstallProcess.StandardError.ReadToEndAsync();
                await mitmproxyInstallProcess.WaitForExitAsync();
                
                logs.AppendLine($"Mitmproxy install exit code: {mitmproxyInstallProcess.ExitCode}");
                if (!string.IsNullOrWhiteSpace(mitmInstallOutput))
                    logs.AppendLine($"Mitmproxy install output: {mitmInstallOutput}");
                if (!string.IsNullOrWhiteSpace(mitmInstallError))
                    logs.AppendLine($"Mitmproxy install error: {mitmInstallError}");
                
                // Check if mitmproxy was installed successfully
                bool mitmAvailable = await PythonService.IsMitmproxyAvailable();
                logs.AppendLine($"Mitmproxy availability check: {(mitmAvailable ? "Available" : "Not available")}");
                return (mitmAvailable, logs.ToString());
            }
            catch (Exception ex)
            {
                logs.AppendLine($"ERROR: {ex.Message}");
                logs.AppendLine(ex.StackTrace);
                Debug.WriteLine($"Error ensuring mitmproxy is installed: {ex.Message}");
                return (false, logs.ToString());
            }
        }
        
        // Install all required dependencies
        public static async Task<(bool Success, string Logs)> InstallDependencies()
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== Dependency Installation Started ===");
            logs.AppendLine($"Timestamp: {DateTime.Now}");
            logs.AppendLine();
            
            try
            {
                // Ensure Python is installed
                logs.AppendLine("Starting Python installation");
                var pythonResult = await EnsurePythonInstalled();
                logs.AppendLine(pythonResult.Logs);
                
                if (!pythonResult.Success)
                {
                    logs.AppendLine("ERROR: Failed to install Python");
                    Debug.WriteLine("Failed to install Python");
                    return (false, logs.ToString());
                }
                
                // Ensure mitmproxy is installed
                logs.AppendLine("\nStarting mitmproxy installation");
                var mitmResult = await EnsureMitmproxyInstalled();
                logs.AppendLine(mitmResult.Logs);
                
                if (!mitmResult.Success)
                {
                    logs.AppendLine("ERROR: Failed to install mitmproxy");
                    Debug.WriteLine("Failed to install mitmproxy");
                    return (false, logs.ToString());
                }
                
                logs.AppendLine("\n=== All dependencies installed successfully ===");
                return (true, logs.ToString());
            }
            catch (Exception ex)
            {
                logs.AppendLine($"\nERROR during dependency installation: {ex.Message}");
                logs.AppendLine(ex.StackTrace);
                Debug.WriteLine($"Error installing dependencies: {ex.Message}");
                return (false, logs.ToString());
            }
        }
        
        // Install Python dependencies required for the application
        public static async Task<(bool Success, string Logs)> InstallPythonDependencies()
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== Python Dependencies Installation Log ===");
            
            try
            {
                // Ensure Python is available before attempting to install dependencies
                if (!await PythonService.IsPythonAvailable())
                {
                    logs.AppendLine("ERROR: Python is not available, cannot install dependencies");
                    Debug.WriteLine("Python is not available, cannot install dependencies");
                    return (false, logs.ToString());
                }
                
                logs.AppendLine("Python is available, installing required packages");
                Debug.WriteLine("Python is available, installing required packages");
                
                string pythonPath = PythonService.GetPythonExecutablePath();
                logs.AppendLine($"Using Python from: {pythonPath}");
                
                // List of required Python packages
                string[] requiredPackages = new[]
                {
                    "tabulate",
                    "mitmproxy"
                };
                
                bool allSuccess = true;
                
                // Install each package using pip
                foreach (var package in requiredPackages)
                {
                    logs.AppendLine($"\nInstalling package: {package}");
                    Debug.WriteLine($"Installing package: {package}");
                    
                    var packageInstallProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pythonPath,
                            Arguments = $"-m pip install {package}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = false
                        }
                    };
                    
                    packageInstallProcess.Start();
                    string packageOutput = await packageInstallProcess.StandardOutput.ReadToEndAsync();
                    string packageError = await packageInstallProcess.StandardError.ReadToEndAsync();
                    await packageInstallProcess.WaitForExitAsync();
                    
                    logs.AppendLine($"Package install exit code: {packageInstallProcess.ExitCode}");
                    if (!string.IsNullOrWhiteSpace(packageOutput))
                        logs.AppendLine($"Package install output: {packageOutput}");
                    if (!string.IsNullOrWhiteSpace(packageError))
                        logs.AppendLine($"Package install error: {packageError}");
                    
                    if (packageInstallProcess.ExitCode != 0)
                    {
                        logs.AppendLine($"WARNING: Failed to install package: {package}");
                        Debug.WriteLine($"Failed to install package: {package}");
                        allSuccess = false;
                    }
                    else
                    {
                        logs.AppendLine($"Successfully installed package: {package}");
                        // Add more detailed success logging
                        Debug.WriteLine($"Successfully installed package: {package}");
                        
                        // Display the version of the installed package for confirmation
                        var versionCheckProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = pythonPath,
                                Arguments = $"-m pip show {package}",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = false
                            }
                        };
                        
                        versionCheckProcess.Start();
                        string versionOutput = await versionCheckProcess.StandardOutput.ReadToEndAsync();
                        await versionCheckProcess.WaitForExitAsync();
                        
                        if (!string.IsNullOrWhiteSpace(versionOutput))
                        {
                            logs.AppendLine($"Package details:\n{versionOutput}");
                            Debug.WriteLine($"Package details:\n{versionOutput}");
                        }
                    }
                }
                
                if (allSuccess)
                {
                    logs.AppendLine("\nAll Python dependencies installed successfully");
                    // Print the final log message to Debug output as well
                    Debug.WriteLine("All Python dependencies installed successfully");
                    
                    // List all installed packages for confirmation
                    var listPackagesProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pythonPath,
                            Arguments = "-m pip list",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = false
                        }
                    };
                    
                    listPackagesProcess.Start();
                    string installedPackages = await listPackagesProcess.StandardOutput.ReadToEndAsync();
                    await listPackagesProcess.WaitForExitAsync();
                    
                    logs.AppendLine("\nInstalled packages:");
                    logs.AppendLine(installedPackages);
                    Debug.WriteLine($"\nInstalled packages:\n{installedPackages}");
                    
                    return (true, logs.ToString());
                }
                else
                {
                    logs.AppendLine("\nWARNING: Some Python dependencies failed to install");
                    return (false, logs.ToString());
                }
            }
            catch (Exception ex)
            {
                logs.AppendLine($"ERROR: {ex.Message}");
                logs.AppendLine(ex.StackTrace);
                Debug.WriteLine($"Error installing Python dependencies: {ex.Message}");
                return (false, logs.ToString());
            }
        }
        
        // Install all required dependencies including Python packages
        public static async Task<(bool Success, string Logs)> InstallAllDependencies()
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== Complete Dependency Installation Started ===");
            logs.AppendLine($"Timestamp: {DateTime.Now}");
            logs.AppendLine();
            
            try
            {
                // First ensure Python and mitmproxy are installed
                var dependenciesResult = await InstallDependencies();
                logs.AppendLine(dependenciesResult.Logs);
                
                if (!dependenciesResult.Success)
                {
                    logs.AppendLine("ERROR: Failed to install basic dependencies");
                    return (false, logs.ToString());
                }
                
                // Then install Python packages
                logs.AppendLine("\nStarting Python packages installation");
                var packagesResult = await InstallPythonDependencies();
                logs.AppendLine(packagesResult.Logs);
                
                if (!packagesResult.Success)
                {
                    logs.AppendLine("WARNING: Some Python packages failed to install");
                    // Continue anyway as some non-critical packages might have failed
                }
                
                logs.AppendLine("\n=== Complete dependency installation finished ===");
                return (true, logs.ToString());
            }
            catch (Exception ex)
            {
                logs.AppendLine($"\nERROR during complete dependency installation: {ex.Message}");
                logs.AppendLine(ex.StackTrace);
                Debug.WriteLine($"Error installing all dependencies: {ex.Message}");
                return (false, logs.ToString());
            }
        }
    }
}