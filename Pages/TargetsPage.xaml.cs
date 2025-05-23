using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System.Text.RegularExpressions;
using WinUI_V3.Helpers;

namespace WinUI_V3.Pages
{
    public sealed partial class TargetsPage : Page
    {
        // Observable collection to store and display targets
        public ObservableCollection<TargetItem> Targets { get; } = [];
        
        // Track if we're editing an existing item
        private TargetItem? CurrentEditingItem { get; set; }
        
        // Flag to check if Python environment is available
        private bool IsPythonEnvironmentAvailable { get; set; } = true;
        
        // Flag to track when targets are being loaded
        private bool _isLoadingTargets = false;
        
        // Timer for checking server status
        private readonly DispatcherTimer? _statusTimer;
        
        // Update the hardcoded paths to use resolution
        private readonly string DbPath = string.Empty;
        private string ModularPath = string.Empty;
        private readonly string SamplesPath = string.Empty;

        // Regular expression patterns
        private static readonly Regex IdRegex = new(@"""id""\s*:\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex UrlRegex = new(@"""url""\s*:\s*""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex ModificationTypeRegex = new(@"""modification_type""\s*:\s*""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex StatusCodeRegex = new(@"""status_code""\s*:\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex NullStatusCodeRegex = new(@"""status_code""\s*:\s*null", RegexOptions.Compiled);
        private static readonly Regex TargetStatusCodeRegex = new(@"""target_status_code""\s*:\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex NullTargetStatusCodeRegex = new(@"""target_status_code""\s*:\s*null", RegexOptions.Compiled);
        private static readonly Regex DynamicCodeRegex = new(@"""dynamic_code""\s*:", RegexOptions.Compiled);
        private static readonly Regex StaticResponseRegex = new(@"""static_response""\s*:", RegexOptions.Compiled);
        private static readonly Regex IsEnabledRegex = new(@"""is_enabled""\s*:\s*(\d+)", RegexOptions.Compiled);

        public TargetsPage()
        {
            try
            {
                // Initialize paths first with resolution
                DbPath = ResolvePath("targets.db");
                ModularPath = ResolvePath("mitm_modular");
                SamplesPath = ResolvePath(Path.Combine("mitm_modular", "samples"));
                
                Debug.WriteLine($"Resolved paths - DB: {DbPath}, Modular: {ModularPath}, Samples: {SamplesPath}");
                
                // Initialize component in its own try-catch to isolate any XAML loading errors
            try
            {
                this.InitializeComponent();
                    // Don't set DataContext at all - avoid binding completely
                }
                catch (Exception initEx)
                {
                    Debug.WriteLine($"*** ERROR initializing component: {initEx.Message}");
                    // We still continue as the page might still be functional
                }

                // Initialize non-UI objects
                _statusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(30)
                };
                _statusTimer.Tick += StatusTimer_Tick;
                
                // Register for Loaded event - defer all UI interaction to this event
                this.Loaded += TargetsPage_Loaded;
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash
                Debug.WriteLine($"*** CRITICAL ERROR initializing TargetsPage: {ex.Message}");
            }
        }

        private void TargetsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load targets data
                LoadTargets();
                
                // Manually update the list view instead of relying on data binding
                UpdateTargetsList();
                
                // Check server status immediately and start the timer
                UpdateServerStatus();
                _statusTimer?.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TargetsPage_Loaded: {ex.Message}");
                
                // Now it's safe to show error dialogs
                try
                {
                ShowErrorMessage("Initialization Error", $"Failed to initialize the page: {ex.Message}");
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"Failed to show error dialog: {dialogEx.Message}");
                }
            }
        }
        
        private void StatusTimer_Tick(object? sender, object e)
        {
            UpdateServerStatus();
        }
        
        private async void UpdateServerStatus()
        {
            try
            {
                bool isRunning = await ProxyService.IsProxyRunning();
                
                // Update the UI elements based on server status
                if (isRunning)
                {
                    StatusDot.Fill = new SolidColorBrush(Colors.Green);
                    ServerStatusText.Text = "Port 45871";
                    ToolTipService.SetToolTip(ServerStatusIndicator, "Server is running and ready to intercept traffic on 127.0.0.1:45871");
                }
                else
                {
                    StatusDot.Fill = new SolidColorBrush(Colors.Red);
                    ServerStatusText.Text = "Server Not Running";
                    ToolTipService.SetToolTip(ServerStatusIndicator, "Server is not running. Start the proxy server in the Settings page.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating server status: {ex.Message}");
                StatusDot.Fill = new SolidColorBrush(Colors.Red);
                ServerStatusText.Text = "Status Check Error";
                ToolTipService.SetToolTip(ServerStatusIndicator, "Error checking server status. Please check logs for details.");
            }
        }
        
        // Load targets from the database with improved error handling
        private async void LoadTargets()
        {
            try
            {
                // Set the loading flag to prevent toggle events from firing
                _isLoadingTargets = true;
                
                // First, add a placeholder item indicating we're loading
                Targets.Clear();
                Targets.Add(new TargetItem
                {
                    Id = 0,
                    TargetUrl = "Loading targets...",
                    HttpStatus = "Any",
                    IsStaticResponse = true,
                    ResponseContent = "{}",
                    IsEnabled = true
                });
                
                // Update UI immediately to show loading state
                UpdateTargetsList();
                
                Debug.WriteLine($"Starting target loading process");
                Debug.WriteLine($"Database path: {DbPath}, exists: {File.Exists(DbPath)}");
                Debug.WriteLine($"Module path: {ModularPath}, exists: {Directory.Exists(ModularPath)}");
                
                // Check if Python is available before attempting to use it
                bool pythonAvailable = await IsPythonAvailable();
                Debug.WriteLine($"Python available: {pythonAvailable}");
                
                if (!pythonAvailable)
                {
                    IsPythonEnvironmentAvailable = false;
                    Targets.Clear();
                    Targets.Add(new TargetItem
                    {
                        Id = 0,
                        TargetUrl = "Python not found. Please install Python and ensure it's available in your PATH.",
                        HttpStatus = "Any",
                        IsStaticResponse = true,
                        ResponseContent = "{}",
                        IsEnabled = true
                    });
                    
                    UpdateTargetsList();
                    
                    // Show error dialog
                    await Task.Delay(100); // Brief delay to ensure UI is updated
                    ShowErrorMessage(
                        "Python Not Found", 
                        "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                    );
                    
                    _isLoadingTargets = false;
                    return;
                }
                
                // If the database file doesn't exist, create an empty one or show a message
                if (!File.Exists(DbPath))
                {
                    Debug.WriteLine($"Database file not found at: {DbPath}");
                    // Try to find any db file in the directory
                    string? dbDir = Path.GetDirectoryName(DbPath);
                    if (dbDir != null && Directory.Exists(dbDir))
                    {
                        var dbFiles = Directory.GetFiles(dbDir, "*.db");
                        if (dbFiles.Length > 0)
                        {
                            Debug.WriteLine($"Found other DB files: {string.Join(", ", dbFiles)}");
                        }
                        else
                        {
                            Debug.WriteLine($"No .db files found in {dbDir}");
                        }
                    }
                    
                    // Add a sample item for demonstration
                    Targets.Clear();
                    Targets.Add(new TargetItem
                    {
                        Id = 0,
                        TargetUrl = "No targets found. Database file not found at: " + DbPath,
                        HttpStatus = "Any",
                        IsStaticResponse = true,
                        ResponseContent = "{}",
                        IsEnabled = true
                    });
                    
                    UpdateTargetsList();
                    _isLoadingTargets = false;
                    return;
                }
                
                try
                {
                    Debug.WriteLine("Running CLI command to get targets");
                var results = await RunCliCommandAsync("json-list-all");
                    Debug.WriteLine($"Got {results.Count} targets from CLI");
                    
                    // Clear the loading placeholder
                    Targets.Clear();
                
                // For each target returned by the CLI, add it to our observable collection
                foreach (var target in results)
                {
                    Targets.Add(target);
                }
                
                // If no targets were found, add a placeholder
                if (Targets.Count == 0)
                {
                        Debug.WriteLine("No targets found, adding placeholder");
                    Targets.Add(new TargetItem
                    {
                        Id = 0,
                        TargetUrl = "No targets found. Add your first target using the 'Add Target' button.",
                        HttpStatus = "Any",
                        IsStaticResponse = true,
                        ResponseContent = "{}",
                        IsEnabled = true
                    });
                }
                }
                catch (Exception cliEx)
                {
                    Debug.WriteLine($"Error running CLI command: {cliEx.Message}");
                    
                    // Add a placeholder item indicating the error
                    Targets.Clear();
                    Targets.Add(new TargetItem
                    {
                        Id = 0,
                        TargetUrl = $"Error loading targets: {cliEx.Message}",
                        HttpStatus = "Any",
                        IsStaticResponse = true,
                        ResponseContent = "{}",
                        IsEnabled = true
                    });
                    
                    // Show error dialog
                    ShowErrorMessage("Error Loading Targets", $"Failed to load targets: {cliEx.Message}");
                }
                
                // Update the ListView with the current items
                UpdateTargetsList();
                
                // Reset the loading flag
                _isLoadingTargets = false;
            }
            catch (Exception ex)
            {
                // Log the error but don't crash
                Debug.WriteLine($"Error loading targets: {ex.Message}");
                
                // Add a placeholder item indicating the error
                Targets.Clear();
                Targets.Add(new TargetItem
                {
                    Id = 0,
                    TargetUrl = $"Error loading targets: {ex.Message}",
                    HttpStatus = "Any",
                    IsStaticResponse = true,
                    ResponseContent = "{}",
                    IsEnabled = true
                });
                
                // Update the ListView with the error item
                UpdateTargetsList();
                
                // Show error dialog
                ShowErrorMessage("Error Loading Targets", $"Failed to load targets: {ex.Message}");
                
                // Reset the loading flag
                _isLoadingTargets = false;
            }
        }
        
        // Check if Python is available with better diagnostics
        private async Task<bool> IsPythonAvailable()
        {
            try
            {
                return await PythonService.IsPythonAvailable();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in IsPythonAvailable: {ex.Message}");
                return false;
            }
        }

        // Check if a Python module is available
        private static async Task<bool> IsModuleAvailable(string moduleName)
        {
            try
            {
                Debug.WriteLine($"Checking for Python module availability: {moduleName}");
                return await PythonService.IsModuleAvailable(moduleName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking module availability: {ex.Message}");
                return false;
            }
        }
        
        // Show an error message dialog
        private async void ShowErrorMessage(string title, string message)
        {
            try
            {
                await DialogService.ShowErrorDialog(this.XamlRoot, title, message);
            }
            catch (Exception ex)
            {
                // If we can't even show the dialog, log to debug output
                Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
                Debug.WriteLine($"Original error: {title} - {message}");
            }
        }

        // Update the RunCliCommandAsync method
        private async Task<ObservableCollection<TargetItem>> RunCliCommandAsync(string command, params string[] args)
        {
            var results = new ObservableCollection<TargetItem>();
            
            if (!IsPythonEnvironmentAvailable)
            {
                // Don't attempt to run Python commands if we know Python isn't available
                return results;
            }
            
            try
            {
                string pythonPath = PythonService.GetPythonExecutablePath();
                Debug.WriteLine($"Using Python executable: {pythonPath}");
                
                // Get app directory and use tools subfolder
                string appDirectory = PythonService.GetAppFolder();
                string basePath = Path.Combine(appDirectory, "tools");
                ModularPath = Path.Combine(basePath, "mitm_modular");
                
                // Validate paths
                string? modularDirPath = Path.GetDirectoryName(ModularPath);
                if (modularDirPath == null || !Directory.Exists(modularDirPath))
                {
                    Debug.WriteLine($"Modular directory not found: {modularDirPath ?? "null"}");
                    throw new DirectoryNotFoundException($"Directory not found: {modularDirPath ?? "null"}");
                }
                
                // Check if the mitm_modular directory exists
                Debug.WriteLine($"Checking if module directory exists at: {ModularPath}, exists: {Directory.Exists(ModularPath)}");
                if (!Directory.Exists(ModularPath))
                {
                    Debug.WriteLine($"Module directory not found: {ModularPath}");
                    throw new DirectoryNotFoundException($"Module directory not found: {ModularPath}");
                }
                
                // Build the command line arguments
                var argsList = new List<string>
                {
                    // Add command as first argument (list, add, delete, etc.)
                    command
                };
                
                // For add command, ensure URL is properly handled
                if (command == "add" && args.Length > 0)
                {
                    // Handle URL - removing existing quotes if present
                    string targetUrl = args[0].Trim('"', '\'');
                    
                    // For URLs with spaces, quote them
                    if (targetUrl.Contains(' '))
                    {
                        argsList.Add($"\"{targetUrl}\"");
                    }
                    else
                    {
                        argsList.Add(targetUrl);
                    }
                    
                    // Add remaining args (skip url)
                    for (int i = 1; i < args.Length; i++)
                    {
                        argsList.Add(args[i]);
                    }
                }
                else
                {
                    // For all other commands just add all args
                    argsList.AddRange(args);
                }
                
                // Join all arguments with spaces
                string cliArguments = string.Join(" ", argsList);
                Debug.WriteLine($"CLI arguments: {cliArguments}");
                
                // Use the '-m' flag to directly invoke the module as specified in the README
                ProcessStartInfo startInfo = new()
                {
                    FileName = pythonPath,
                    WorkingDirectory = basePath, // Set working directory to the tools path
                    Arguments = $"-m mitm_modular.cli {cliArguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                Debug.WriteLine($"Running command: {startInfo.FileName} {startInfo.Arguments}");
                Debug.WriteLine($"Working directory: {startInfo.WorkingDirectory}");

                // Start the process with extensive error logging
                using var process = new Process { StartInfo = startInfo };
                try
                {
                    Debug.WriteLine("Starting Python process");
                    process.Start();
                    Debug.WriteLine("Python process started");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error starting Python process: {ex.Message}");
                    throw new Exception($"Error starting Python process: {ex.Message}", ex);
                }

                // Read output in real time for better diagnostics
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Debug.WriteLine($"Python output: {e.Data}");
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Debug.WriteLine($"Python error: {e.Data}");
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                string output = outputBuilder.ToString();
                string errors = errorBuilder.ToString();

                Debug.WriteLine($"Python process exit code: {process.ExitCode}");

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine("Command failed with error output:");
                    Debug.WriteLine(errors);
                    throw new Exception($"Error executing CLI command ({process.ExitCode}): {errors}");
                }

                // Add this right after reading the CLI output
                Debug.WriteLine("Full CLI output:");
                Debug.WriteLine(output);
                Debug.WriteLine("------ End of CLI output ------");

                // Handle json-list and json-list-all commands
                if ((command == "json-list" || command == "json-list-all"))
                {
                    Debug.WriteLine($"Processing {command} output");

                    // Clean up the output - remove any non-JSON content like Python debug messages
                    string jsonOutput = output;
                    int jsonStart = jsonOutput.IndexOf('[');
                    int jsonEnd = jsonOutput.LastIndexOf(']');

                    if (jsonStart >= 0 && jsonEnd > jsonStart)
                    {
                        jsonOutput = jsonOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        Debug.WriteLine($"Extracted JSON: {jsonOutput}");

                        try
                        {
                            // Use our manual parsing approach which is more reliable in Release mode
                            Debug.WriteLine("Using manual JSON parsing approach");
                            var manualTargets = ParseJsonManually(jsonOutput);
                            Debug.WriteLine($"Manual parsing returned {manualTargets.Count} targets");

                            foreach (var target in manualTargets)
                            {
                                // Create target item from manually parsed data
                                var targetItem = new TargetItem
                                {
                                    Id = target.TryGetValue("id", out var idValue) ? Convert.ToInt32(idValue) : 0,
                                    TargetUrl = target.TryGetValue("url", out var urlValue) ? urlValue?.ToString() ?? string.Empty : string.Empty,
                                    HttpStatus = target.TryGetValue("status_code", out var statusValue) ? statusValue?.ToString() : null,
                                    TargetHttpStatus = target.TryGetValue("target_status_code", out var targetStatusValue) ? targetStatusValue?.ToString() : null,
                                    ModificationType = target.TryGetValue("modification_type", out var modificationTypeValue) ? modificationTypeValue?.ToString() ?? "static" : "static",
                                    IsStaticResponse = target.TryGetValue("modification_type", out var staticModValue) && staticModValue?.ToString() == "static",
                                    IsNoModification = target.TryGetValue("modification_type", out var noneModValue) && noneModValue?.ToString() == "none",
                                    ResponseContent = target.TryGetValue("modification_type", out var respModType) ? 
                                        (respModType?.ToString() == "dynamic" && target.TryGetValue("dynamic_code", out _) ? "Dynamic code" :
                                         respModType?.ToString() == "static" && target.TryGetValue("static_response", out _) ? "Static response" : string.Empty)
                                        : string.Empty,
                                    IsEnabled = target.TryGetValue("is_enabled", out var enabledValue) && Convert.ToInt32(enabledValue) == 1
                                };

                                results.Add(targetItem);
                                Debug.WriteLine($"Added targetItem to results: {targetItem.Id}, {targetItem.TargetUrl}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                            Debug.WriteLine($"Exception details: {ex}");
                            throw new Exception($"Error parsing JSON: {ex.Message}", ex);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Could not find valid JSON array in output");
                    }
                }
                // Handle view command
                else if (command == "view" && args.Length > 0)
                {
                    try
                    {
                        // If this is a view command, parse the detailed output
                        int targetId = int.Parse(args[0]);
                        string type = output.Contains("Dynamic Code:") ? "dynamic" : "static";
                        string content = string.Empty;

                        if (type == "dynamic" && output.Contains("Dynamic Code:"))
                        {
                            var parts = output.Split(["Dynamic Code:", "-------------"], StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 2)
                            {
                                content = parts[2].Trim();
                            }
                        }
                        else if (type == "static" && output.Contains("Static Response:"))
                        {
                            var parts = output.Split(["Static Response:", "---------------"], StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 2)
                            {
                                content = parts[2].Trim();
                            }
                        }

                        // Get the target from existing list or create a new one
                        var target = results.FirstOrDefault(t => t.Id == targetId) ?? new TargetItem { Id = targetId };
                        target.IsStaticResponse = type == "static";
                        target.ResponseContent = content;

                        if (!results.Contains(target))
                        {
                            results.Add(target);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing view output: {ex.Message}");
                    }
                }
                // Keep the old table parsing logic for any other commands
                else if (command == "list")
                {
                    // Parse the table output (your existing code)
                    // ...
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run CLI command: {ex.Message}");
                // Re-throw to be handled by caller
                throw new Exception($"Failed to run CLI command: {ex.Message}", ex);
            }
            
            return results;
        }

        // Get full target details using the view command
        private async Task<string> GetFullTargetDetailsAsync(int targetId)
        {
            try
            {
                var targets = await RunCliCommandAsync("view", targetId.ToString());
                if (targets.Count > 0)
                {
                    return targets[0].ResponseContent;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting target details: {ex.Message}");
                // Return a message indicating the error
                return $"Error: {ex.Message}";
            }
            
            return string.Empty;
        }

        private async void AddTargetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if Python environment is available
                if (!IsPythonEnvironmentAvailable)
                {
                    ShowErrorMessage(
                        "Python Not Found", 
                        "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                    );
                    return;
                }
                
                // Reset dialog fields
                if (TargetUrlTextBox != null)
                TargetUrlTextBox.Text = string.Empty;
                
                if (HttpStatusComboBox != null)
                HttpStatusComboBox.SelectedIndex = 0; // Default to "Any Status"
                
                if (TargetHttpStatusComboBox != null)
                TargetHttpStatusComboBox.SelectedIndex = 0; // Default to "No Change"
                
                if (StaticResponseRadioButton != null)
                StaticResponseRadioButton.IsChecked = true;
                
                if (NoModificationRadioButton != null)
                    NoModificationRadioButton.IsChecked = false;
                
                if (DynamicResponseRadioButton != null)
                    DynamicResponseRadioButton.IsChecked = false;
                
                if (StaticResponseTextBox != null)
                StaticResponseTextBox.Text = string.Empty;
                
                if (DynamicResponseTextBox != null)
                    DynamicResponseTextBox.Text = string.Empty;
                
                // Set initial visibility
                if (StaticResponsePanel != null)
                    StaticResponsePanel.Visibility = Visibility.Visible;
                
                if (DynamicResponsePanel != null)
                    DynamicResponsePanel.Visibility = Visibility.Collapsed;
                
                CurrentEditingItem = null;
                
                // Show the dialog
                if (AddTargetDialog != null)
                await AddTargetDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing add target dialog: {ex.Message}");
                ShowErrorMessage("Error", $"Failed to show add target dialog: {ex.Message}");
            }
        }

        private async void EditTargetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is TargetItem targetItem)
                {
                    // Don't allow editing the placeholder item
                    if (targetItem.Id == 0)
                    {
                        return;
                    }
                    
                    // Check if Python environment is available
                    if (!IsPythonEnvironmentAvailable)
                    {
                        ShowErrorMessage(
                            "Python Not Found", 
                            "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                        );
                        return;
                    }
                    
                    // Populate dialog with existing values
                    if (TargetUrlTextBox != null)
                    TargetUrlTextBox.Text = targetItem.TargetUrl;
                    
                    // Find and select the matching HTTP status
                    if (HttpStatusComboBox != null)
                    {
                    if (!string.IsNullOrEmpty(targetItem.HttpStatus))
                        {
                            foreach (ComboBoxItem item in HttpStatusComboBox.Items.Cast<ComboBoxItem>())
                        {
                                if (item.Tag != null && item.Tag.ToString() == targetItem.HttpStatus)
                            {
                                HttpStatusComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Set to "Any Status"
                        HttpStatusComboBox.SelectedIndex = 0;
                        }
                    }
                    
                    // Find and select the matching target HTTP status
                    if (TargetHttpStatusComboBox != null)
                    {
                    if (!string.IsNullOrEmpty(targetItem.TargetHttpStatus))
                        {
                            foreach (ComboBoxItem item in TargetHttpStatusComboBox.Items.Cast<ComboBoxItem>())
                        {
                                if (item.Tag != null && item.Tag.ToString() == targetItem.TargetHttpStatus)
                            {
                                TargetHttpStatusComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Set to "No Change"
                        TargetHttpStatusComboBox.SelectedIndex = 0;
                        }
                    }
                    
                    // Set response type
                    if (targetItem.ResponseContent == null || targetItem.ResponseContent.Trim() == string.Empty)
                    {
                        if (NoModificationRadioButton != null)
                            NoModificationRadioButton.IsChecked = true;
                        
                        if (StaticResponsePanel != null)
                            StaticResponsePanel.Visibility = Visibility.Collapsed;
                        
                        if (DynamicResponsePanel != null)
                            DynamicResponsePanel.Visibility = Visibility.Collapsed;
                    }
                    else if (targetItem.IsStaticResponse)
                    {
                        if (StaticResponseRadioButton != null)
                        StaticResponseRadioButton.IsChecked = true;
                        
                        if (StaticResponsePanel != null)
                            StaticResponsePanel.Visibility = Visibility.Visible;
                        
                        if (DynamicResponsePanel != null)
                            DynamicResponsePanel.Visibility = Visibility.Collapsed;
                        
                        if (StaticResponseTextBox != null)
                        StaticResponseTextBox.Text = targetItem.ResponseContent;
                    }
                    else
                    {
                        if (DynamicResponseRadioButton != null)
                        DynamicResponseRadioButton.IsChecked = true;
                        
                        if (StaticResponsePanel != null)
                            StaticResponsePanel.Visibility = Visibility.Collapsed;
                        
                        if (DynamicResponsePanel != null)
                            DynamicResponsePanel.Visibility = Visibility.Visible;
                        
                        if (DynamicResponseTextBox != null)
                        DynamicResponseTextBox.Text = targetItem.ResponseContent;
                    }
                    
                    CurrentEditingItem = targetItem;
                    
                    // Show the dialog
                    if (AddTargetDialog != null)
                    await AddTargetDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing edit target dialog: {ex.Message}");
                ShowErrorMessage("Error", $"Failed to show edit target dialog: {ex.Message}");
            }
        }

        private async void DeleteTargetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is TargetItem targetItem)
                {
                    // Don't allow deleting the placeholder item
                    if (targetItem.Id == 0)
                    {
                        return;
                    }
                    
                    // Check if Python environment is available
                    if (!IsPythonEnvironmentAvailable)
                    {
                        ShowErrorMessage(
                            "Python Not Found", 
                            "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                        );
                        return;
                    }
                    
                    // Show confirmation dialog
                    ContentDialog confirmDialog = new()
                    {
                        Title = "Confirm Delete",
                        Content = $"Are you sure you want to delete the target '{targetItem.TargetUrl}'?",
                        PrimaryButtonText = "Delete",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    
                    var result = await confirmDialog.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        return;
                    }
                    
                    // Run the CLI command to delete the target without the --db parameter
                    await RunCliCommandAsync("delete", targetItem.Id.ToString());
                    
                    // Remove from our collection
                    Targets.Remove(targetItem);
                    
                    // If no targets left, add a placeholder
                    if (Targets.Count == 0)
                    {
                        Targets.Add(new TargetItem
                        {
                            Id = 0,
                            TargetUrl = "No targets found. Add your first target using the 'Add Target' button.",
                            HttpStatus = "Any",
                            IsStaticResponse = true,
                            ResponseContent = "{}",
                            IsEnabled = true
                        });
                    }
                    
                    // Refresh the targets list from the database
                    await Task.Delay(100); // Brief delay to ensure database operation completes
                    LoadTargets();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting target: {ex.Message}");
                ShowErrorMessage("Error Deleting Target", $"Failed to delete target: {ex.Message}");
            }
        }

        private async void EnableSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // Skip toggling if we're currently loading targets
            if (_isLoadingTargets)
                return;
                
            try
            {
                if (sender is ToggleSwitch toggleSwitch && toggleSwitch.Tag is TargetItem targetItem)
                {
                    // Don't allow toggling the placeholder item
                    if (targetItem.Id == 0)
                    {
                        // Reset the toggle switch to its original state without triggering another event
                        toggleSwitch.Toggled -= EnableSwitch_Toggled;
                        toggleSwitch.IsOn = true;
                        toggleSwitch.Toggled += EnableSwitch_Toggled;
                        return;
                    }
                    
                    // Check if Python environment is available
                    if (!IsPythonEnvironmentAvailable)
                    {
                        // Reset the toggle switch to its original state without triggering another event
                        toggleSwitch.Toggled -= EnableSwitch_Toggled;
                        toggleSwitch.IsOn = !toggleSwitch.IsOn;
                        toggleSwitch.Toggled += EnableSwitch_Toggled;
                        
                        ShowErrorMessage(
                            "Python Not Found", 
                            "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                        );
                        return;
                    }
                    
                    // Get the toggle state from the switch
                    bool isEnabled = toggleSwitch.IsOn;
                    
                    // Run the CLI command to enable/disable the target without the --db parameter
                    string command = isEnabled ? "enable" : "disable";
                    await RunCliCommandAsync(command, targetItem.Id.ToString());
                    
                    // Update the model
                    targetItem.IsEnabled = isEnabled;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling target: {ex.Message}");
                ShowErrorMessage("Error Toggling Target", $"Failed to toggle target state: {ex.Message}");
                
                // Reset the toggle switch to its original state without triggering another event
                if (sender is ToggleSwitch toggleSwitch && toggleSwitch.Tag is TargetItem targetItem)
                {
                    toggleSwitch.Toggled -= EnableSwitch_Toggled;
                    toggleSwitch.IsOn = targetItem.IsEnabled;
                    toggleSwitch.Toggled += EnableSwitch_Toggled;
                }
            }
        }

        // Update the radio button selection changed handler to handle our new layout
        private void ResponseTypeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This method is deprecated - individual radio button Checked events are used instead
            Debug.WriteLine("ResponseTypeRadioButtons_SelectionChanged called but is deprecated");
        }
        
        // Handle radio button checked state changes
        private void NoModificationRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StaticResponsePanel != null)
                    StaticResponsePanel.Visibility = Visibility.Collapsed;
                
                if (DynamicResponsePanel != null)
                    DynamicResponsePanel.Visibility = Visibility.Collapsed;
                
                // Ensure target status combo box is still enabled
                if (TargetHttpStatusComboBox != null)
                {
                    // We don't want to force a selection, but we should encourage one
                    // for "none" modification type
                    if (TargetHttpStatusComboBox.SelectedIndex == 0) // "No Change" selected
                    {
                        // Maybe highlight it somehow or show a hint
                        // For now we'll just log it
                        Debug.WriteLine("No modification selected with 'No Change' status code");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NoModificationRadioButton_Checked: {ex.Message}");
            }
        }
        
        private void StaticResponseRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StaticResponseRadioButton?.IsChecked == true && StaticResponsePanel != null && DynamicResponsePanel != null)
                {
                    StaticResponsePanel.Visibility = Visibility.Visible;
                    DynamicResponsePanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StaticResponseRadioButton_Checked: {ex.Message}");
            }
        }
        
        private void DynamicResponseRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DynamicResponseRadioButton?.IsChecked == true && StaticResponsePanel != null && DynamicResponsePanel != null)
                {
                    StaticResponsePanel.Visibility = Visibility.Collapsed;
                    DynamicResponsePanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DynamicResponseRadioButton_Checked: {ex.Message}");
            }
        }
        
        // Sample button click handlers
        private async void StaticSampleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First close the parent dialog to avoid multiple open dialogs
                AddTargetDialog?.Hide();
                
                string samplePath = Path.Combine(SamplesPath, "static.json");
                
                if (File.Exists(samplePath))
                {
                    string sampleContent = await File.ReadAllTextAsync(samplePath);
                    if (StaticSampleTextBox != null)
                        StaticSampleTextBox.Text = sampleContent;
                    
                    // Show the sample dialog
                    if (StaticSampleDialog != null)
                    {
                        ContentDialogResult result = await StaticSampleDialog.ShowAsync();
                        
                        // After the sample dialog is closed, re-open the add target dialog
                        if (AddTargetDialog != null)
                        {
                            await AddTargetDialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    // If we can't find the sample file, first re-open the parent dialog
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                    
                    // Then show the error message
                    ShowErrorMessage("Sample Not Found", $"Could not find the sample file at: {samplePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing static sample: {ex.Message}");
                
                // Make sure the parent dialog is re-opened
                try
                {
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"Error re-opening add target dialog: {dialogEx.Message}");
                }
                
                // Then show the error message
                ShowErrorMessage("Error", $"Failed to show static sample: {ex.Message}");
            }
        }
        
        private async void DynamicSampleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First close the parent dialog to avoid multiple open dialogs
                AddTargetDialog?.Hide();
                
                string samplePath = Path.Combine(SamplesPath, "dynamic.py");
                
                if (File.Exists(samplePath))
                {
                    string sampleContent = await File.ReadAllTextAsync(samplePath);
                    if (DynamicSampleTextBox != null)
                        DynamicSampleTextBox.Text = sampleContent;
                    
                    // Show the sample dialog
                    if (DynamicSampleDialog != null)
                    {
                        ContentDialogResult result = await DynamicSampleDialog.ShowAsync();
                        
                        // After the sample dialog is closed, re-open the add target dialog
                        if (AddTargetDialog != null)
                        {
                            await AddTargetDialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    // If we can't find the sample file, first re-open the parent dialog
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                    
                    // Then show the error message
                    ShowErrorMessage("Sample Not Found", $"Could not find the sample file at: {samplePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing dynamic sample: {ex.Message}");
                
                // Make sure the parent dialog is re-opened
                try
                {
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"Error re-opening add target dialog: {dialogEx.Message}");
                }
                
                // Then show the error message
                ShowErrorMessage("Error", $"Failed to show dynamic sample: {ex.Message}");
            }
        }

        private async void AddTargetDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // Get values from the dialog
                if (TargetUrlTextBox == null)
                {
                    args.Cancel = true;
                    return;
                }
                
                string targetUrl = TargetUrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(targetUrl))
                {
                    args.Cancel = true; // Cancel first before attempting to show dialog
                    sender.Hide(); // Hide the dialog before showing an error
                    
                    try
                    {
                        // Show error message
                        ContentDialog errorDialog = new()
                        {
                            Title = "Invalid Input",
                            Content = "Target URL or Match String cannot be empty.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        
                        await errorDialog.ShowAsync();
                        
                        // Re-open the original dialog
                        if (AddTargetDialog != null)
                        {
                            await AddTargetDialog.ShowAsync();
                        }
                    }
                    catch (Exception errorEx)
                    {
                        Debug.WriteLine($"Failed to show error dialog: {errorEx.Message}");
                        Debug.WriteLine("Original error: Invalid Input - Target URL or Match String cannot be empty.");
                    }
                    
                    return;
                }
                
                // Get match HTTP status and target HTTP status
                string httpStatus = "Any";
                if (HttpStatusComboBox?.SelectedItem is ComboBoxItem httpStatusItem && httpStatusItem.Tag != null)
                {
                    httpStatus = httpStatusItem.Tag.ToString() ?? "Any";
                }
                
                string targetHttpStatus = "NoChange";
                if (TargetHttpStatusComboBox?.SelectedItem is ComboBoxItem targetStatusItem && targetStatusItem.Tag != null)
                {
                    targetHttpStatus = targetStatusItem.Tag.ToString() ?? "NoChange";
                }
                
                // Determine the response type
                bool isNoModification = NoModificationRadioButton?.IsChecked == true;
                bool isStaticResponse = StaticResponseRadioButton?.IsChecked == true;
                
                string responseContent = string.Empty;
                
                if (!isNoModification)
                {
                    if (isStaticResponse && StaticResponseTextBox != null)
                    {
                        responseContent = StaticResponseTextBox.Text.Trim();
                        // If it's empty, set it to a valid empty JSON object
                        if (string.IsNullOrWhiteSpace(responseContent))
                        {
                            responseContent = "{}";
                        }
                    }
                    else if (DynamicResponseTextBox != null)
                    {
                        responseContent = DynamicResponseTextBox.Text.Trim();
                    }
                }
                    
                // Validate JSON if this is a static response
                if (isStaticResponse && !string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {
                        // Try to parse the JSON to make sure it's valid
                        JsonDocument.Parse(responseContent);
                    }
                    catch (JsonException jsonEx)
                    {
                        args.Cancel = true; // Cancel first before attempting to show dialog
                        sender.Hide(); // Hide the dialog before showing an error
                        
                        try
                        {
                            // Show error message
                            ContentDialog errorDialog = new()
                            {
                                Title = "Invalid JSON",
                                Content = $"The static response contains invalid JSON: {jsonEx.Message}",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            
                            await errorDialog.ShowAsync();
                            
                            // Re-open the original dialog
                            if (AddTargetDialog != null)
                            {
                                await AddTargetDialog.ShowAsync();
                            }
                        }
                        catch (Exception errorEx)
                        {
                            Debug.WriteLine($"Failed to show error dialog: {errorEx.Message}");
                            Debug.WriteLine($"Original error: Invalid JSON - {jsonEx.Message}");
                        }
                        
                        return;
                    }
                }
                
                if (CurrentEditingItem != null && CurrentEditingItem.Id != 0)
                {
                    try
                {
                    // Delete the existing target and add a new one (since there's no direct edit function in CLI)
                    await RunCliCommandAsync("delete", CurrentEditingItem.Id.ToString());
                    }
                    catch (Exception ex)
                    {
                        ShowErrorMessage("Error Deleting Target", $"Failed to delete existing target: {ex.Message}");
                        args.Cancel = true;
                        return;
                    }
                }
                
                // If we're only changing the status code (no modification to content)
                if (isNoModification)
                {
                    if (targetHttpStatus == "NoChange")
                    {
                        // No changes selected - show a message
                        args.Cancel = true; // Cancel first before attempting to show dialog
                        sender.Hide(); // Hide the dialog before showing an error
                        
                        try
                        {
                            // Show error message
                            ContentDialog errorDialog = new()
                            {
                                Title = "No Changes Selected",
                                Content = "Please select either a response modification or a status code change.",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            
                            await errorDialog.ShowAsync();
                            
                            // Re-open the original dialog
                            if (AddTargetDialog != null)
                            {
                                await AddTargetDialog.ShowAsync();
                            }
                        }
                        catch (Exception errorEx)
                        {
                            Debug.WriteLine($"Failed to show error dialog: {errorEx.Message}");
                            Debug.WriteLine("Original error: No Changes Selected - Please select either a response modification or a status code change.");
                        }
                        
                        return;
                    }
                    
                    try
                    {
                        // Use the new 'none' modification type for status-only changes
                        var args_list = new List<string> {
                            targetUrl,
                            "--type", "none",
                            "--target-status", targetHttpStatus
                        };
                        
                        // Add the status code if not "Any"
                        if (httpStatus != "Any")
                        {
                            args_list.Add("--status");
                            args_list.Add(httpStatus ?? "Any");
                        }
                        
                        // Run the CLI command
                        await RunCliCommandAsync("add", [.. args_list]);
                    }
                    catch (Exception ex)
                    {
                        ShowErrorMessage("Error Adding Target", $"Failed to add target with status code change: {ex.Message}");
                        args.Cancel = true;
                        return;
                    }
                }
                // Handle normal content modifications
                else
                {
                    try
                    {
                // Prepare the arguments for the add command
                var args_list = new List<string> {
                            targetUrl,
                    "--type", isStaticResponse ? "static" : "dynamic"
                };
                
                // Add the status code if not "Any"
                if (httpStatus != "Any")
                {
                    args_list.Add("--status");
                            args_list.Add(httpStatus ?? "Any");
                }
                
                // Add the target status code if not "NoChange"
                if (targetHttpStatus != "NoChange")
                {
                    args_list.Add("--target-status");
                            args_list.Add(targetHttpStatus ?? "NoChange");
                }
                
                // Add content based on the type
                if (isStaticResponse)
                {
                    // Create a temporary file for the JSON response
                    string tempFile = Path.GetTempFileName();
                    await File.WriteAllTextAsync(tempFile, responseContent ?? "{}");
                    args_list.Add("--response-file");
                    args_list.Add(tempFile);
                }
                else
                {
                    // Create a temporary file for the Python code
                    string tempFile = Path.GetTempFileName();
                    await File.WriteAllTextAsync(tempFile, responseContent ?? string.Empty);
                    args_list.Add("--code-file");
                    args_list.Add(tempFile);
                }
                
                // Run the CLI command
                        await RunCliCommandAsync("add", [.. args_list]);
                    }
                    catch (Exception ex)
                    {
                        // Check for specific JSON errors in the exception message
                        if (ex.Message.Contains("Invalid JSON") || ex.Message.Contains("JSONDecodeError"))
                        {
                            ShowErrorMessage("Invalid JSON", "The static response contains invalid JSON. Please check your JSON format.");
                        }
                        else
                        {
                            ShowErrorMessage("Error Adding Target", $"Failed to add target: {ex.Message}");
                        }
                        args.Cancel = true;
                        return;
                    }
                }
                
                // Reload the targets
                LoadTargets();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving target: {ex.Message}");
                ShowErrorMessage("Error Saving Target", $"Failed to save target: {ex.Message}");
                args.Cancel = true;
            }
        }

        // Fullscreen button click handlers
        private async void StaticFullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First close the parent dialog to avoid multiple open dialogs
                AddTargetDialog?.Hide();
                
                // Copy content from regular textbox to fullscreen textbox
                if (StaticResponseTextBox != null && StaticFullScreenTextBox != null)
                {
                    StaticFullScreenTextBox.Text = StaticResponseTextBox.Text;
                }
                
                // Show the fullscreen dialog
                if (StaticFullScreenDialog != null)
                {
                    ContentDialogResult result = await StaticFullScreenDialog.ShowAsync();
                    
                    // After the fullscreen dialog is closed, re-open the add target dialog
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing fullscreen static editor: {ex.Message}");
                
                // Make sure the parent dialog is re-opened
                try
                {
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"Error re-opening add target dialog: {dialogEx.Message}");
                }
                
                // Then show the error message
                ShowErrorMessage("Error", $"Failed to show fullscreen editor: {ex.Message}");
            }
        }
        
        private async void DynamicFullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First close the parent dialog to avoid multiple open dialogs
                AddTargetDialog?.Hide();
                
                // Copy content from regular textbox to fullscreen textbox
                if (DynamicResponseTextBox != null && DynamicFullScreenTextBox != null)
                {
                    DynamicFullScreenTextBox.Text = DynamicResponseTextBox.Text;
                }
                
                // Show the fullscreen dialog
                if (DynamicFullScreenDialog != null)
                {
                    ContentDialogResult result = await DynamicFullScreenDialog.ShowAsync();
                    
                    // After the fullscreen dialog is closed, re-open the add target dialog
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing fullscreen dynamic editor: {ex.Message}");
                
                // Make sure the parent dialog is re-opened
                try
                {
                    if (AddTargetDialog != null)
                    {
                        await AddTargetDialog.ShowAsync();
                    }
                }
                catch (Exception dialogEx)
                {
                    Debug.WriteLine($"Error re-opening add target dialog: {dialogEx.Message}");
                }
                
                // Then show the error message
                ShowErrorMessage("Error", $"Failed to show fullscreen editor: {ex.Message}");
            }
        }
        
        // Fullscreen dialog primary button click handlers
        private void StaticFullScreenDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // Copy content from fullscreen textbox back to regular textbox
                if (StaticResponseTextBox != null && StaticFullScreenTextBox != null)
                {
                    StaticResponseTextBox.Text = StaticFullScreenTextBox.Text;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying fullscreen static content: {ex.Message}");
                args.Cancel = true;
                ShowErrorMessage("Error", $"Failed to apply changes: {ex.Message}");
            }
        }
        
        private void DynamicFullScreenDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                // Copy content from fullscreen textbox back to regular textbox
                if (DynamicResponseTextBox != null && DynamicFullScreenTextBox != null)
                {
                    DynamicResponseTextBox.Text = DynamicFullScreenTextBox.Text;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying fullscreen dynamic content: {ex.Message}");
                args.Cancel = true;
                ShowErrorMessage("Error", $"Failed to apply changes: {ex.Message}");
            }
        }

        private void UpdateTargetsList()
        {
            try
            {
                // Clear existing items in the ListView if it exists
                if (TargetsListView != null)
                {
                    TargetsListView.Items.Clear();
                    
                    // Manually create UI elements for each target
                    foreach (var target in Targets)
                    {
                        // Create container grid
                        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                        
                        // Define columns
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        
                        // Target URL section
                        var border = new Border
                        {
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(12, 8, 12, 8),
                            Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                            Opacity = target.IsEnabled ? 1.0 : 0.6
                        };
                        Grid.SetColumn(border, 0);
                        
                        // Create content for border
                        var stackPanel = new StackPanel();
                        
                        // Target URL
                        var urlText = new TextBlock
                        {
                            Text = target.TargetUrl ?? "No URL",
                            TextWrapping = TextWrapping.Wrap
                        };
                        stackPanel.Children.Add(urlText);
                        
                        // Details line (only if not a placeholder)
                        if (target.Id != 0)
                        {
                            var detailsPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(0, 4, 0, 0)
                            };
                            
                            // Type
                            var typeLabel = new TextBlock
                            {
                                Text = "Type: ",
                                FontSize = 12,
                                Opacity = 0.7
                            };
                            detailsPanel.Children.Add(typeLabel);
                            
                            var typeValue = new TextBlock
                            {
                                Text = target.ModificationType,
                                FontSize = 12,
                                Opacity = 0.7,
                                Margin = new Thickness(4, 0, 0, 0)
                            };
                            detailsPanel.Children.Add(typeValue);
                            
                            // Match Status
                            var statusLabel = new TextBlock
                            {
                                Text = " | Match Status: ",
                                FontSize = 12,
                                Opacity = 0.7,
                                Margin = new Thickness(8, 0, 0, 0)
                            };
                            detailsPanel.Children.Add(statusLabel);
                            
                            var statusValue = new TextBlock
                            {
                                Text = target.HttpStatus ?? "Any",
                                FontSize = 12,
                                Opacity = 0.7,
                                Margin = new Thickness(4, 0, 0, 0)
                            };
                            detailsPanel.Children.Add(statusValue);
                            
                            // Response Status
                            var targetStatusLabel = new TextBlock
                            {
                                Text = " | Response Status: ",
                                FontSize = 12,
                                Opacity = 0.7,
                                Margin = new Thickness(8, 0, 0, 0)
                            };
                            detailsPanel.Children.Add(targetStatusLabel);
                            
                            var targetStatusValue = new TextBlock
                            {
                                Text = target.TargetHttpStatus ?? "No Change",
                                FontSize = 12,
                                Opacity = 0.7,
                                Margin = new Thickness(4, 0, 0, 0)
                            };
                            detailsPanel.Children.Add(targetStatusValue);
                            
                            stackPanel.Children.Add(detailsPanel);
                        }
                        
                        border.Child = stackPanel;
                        grid.Children.Add(border);
                        
                        // Only add controls if this is not a placeholder item
                        if (target.Id != 0)
                        {
                            // Toggle switch
                            var toggleSwitch = new ToggleSwitch
                            {
                                MinWidth = 0,
                                Width = 60,
                                Margin = new Thickness(8, 0, 0, 0),
                                IsOn = target.IsEnabled,
                                OffContent = "",
                                OnContent = "",
                                Tag = target
                            };
                            toggleSwitch.Toggled += EnableSwitch_Toggled;
                            ToolTipService.SetToolTip(toggleSwitch, "Enable/Disable Target");
                            Grid.SetColumn(toggleSwitch, 1);
                            grid.Children.Add(toggleSwitch);
                            
                            // Edit button
                            var editButton = new Button
                            {
                                Margin = new Thickness(-8, 0, 0, 0),
                                Tag = target,
                            };
                            editButton.Click += EditTargetButton_Click;
                            ToolTipService.SetToolTip(editButton, "Edit");
                            
                            var editIcon = new FontIcon
                            {
                                Glyph = "\uE70F",
                                FontSize = 16
                            };
                            editButton.Content = editIcon;
                            
                            Grid.SetColumn(editButton, 2);
                            grid.Children.Add(editButton);
                            
                            // Delete button
                            var deleteButton = new Button
                            {
                                Margin = new Thickness(8, 0, 0, 0),
                                Tag = target,
                            };
                            deleteButton.Click += DeleteTargetButton_Click;
                            ToolTipService.SetToolTip(deleteButton, "Delete");
                            
                            var deleteIcon = new FontIcon
                            {
                                Glyph = "\uE74D",
                                FontSize = 16
                            };
                            deleteButton.Content = deleteIcon;
                            
                            Grid.SetColumn(deleteButton, 3);
                            grid.Children.Add(deleteButton);
                        }
                        
                        // Add the whole row to the ListView
                        TargetsListView.Items.Add(grid);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating targets list: {ex.Message}");
            }
        }

        // Add a fallback method for parsing JSON manually if needed
        private static List<Dictionary<string, object>> ParseJsonManually(string json)
        {
            Debug.WriteLine("Starting manual JSON parsing");
            Debug.WriteLine($"JSON input: {json}");
            
            try
            {
                var result = new List<Dictionary<string, object>>();
                
                // Simple manual parsing - this is a fallback only
                if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("["))
                {
                    Debug.WriteLine("JSON is empty or not an array");
                    return result;
                }
                
                // For our specific JSON structure, we need to properly split the JSON array
                string content = json.Trim();
                
                // Remove the outer brackets
                content = content.TrimStart('[').TrimEnd(']').Trim();
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    Debug.WriteLine("JSON array is empty");
                    return result;
                }
                
                // Split the JSON array items (properly handling nested objects/arrays)
                List<string> items = [];
                int startIndex = 0;
                int braceCount = 0;
                
                for (int i = 0; i < content.Length; i++)
                {
                    char c = content[i];
                    
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                    
                    // Check if we've reached the end of an item
                    if (braceCount == 0 && c == '}')
                    {
                        // Add the item to our list
                        string item = content.Substring(startIndex, i - startIndex + 1);
                        items.Add(item);
                        
                        // Skip the comma and any whitespace
                        i++;
                        while (i < content.Length && (content[i] == ',' || char.IsWhiteSpace(content[i])))
                        {
                            i++;
                        }
                        
                        // Set the start index for the next item
                        startIndex = i;
                        i--; // Counteract the for loop increment
                    }
                }
                
                Debug.WriteLine($"Found {items.Count} items in JSON array");
                
                // Process each item
                foreach (var itemJson in items)
                {
                    if (string.IsNullOrWhiteSpace(itemJson)) continue;
                    
                    Debug.WriteLine($"Processing item: {itemJson}");
                    
                    var target = new Dictionary<string, object>();
                    
                    // Try to extract ID
                    var idMatch = IdRegex.Match(itemJson);
                    if (idMatch.Success)
                    {
                        target["id"] = int.Parse(idMatch.Groups[1].Value);
                        Debug.WriteLine($"Found ID: {target["id"]}");
                    }
                    
                    // Try to extract URL
                    var urlMatch = UrlRegex.Match(itemJson);
                    if (urlMatch.Success)
                    {
                        target["url"] = urlMatch.Groups[1].Value;
                        Debug.WriteLine($"Found URL: {target["url"]}");
                    }
                    
                    // Try to extract modification_type
                    var modTypeMatch = ModificationTypeRegex.Match(itemJson);
                    if (modTypeMatch.Success)
                    {
                        target["modification_type"] = modTypeMatch.Groups[1].Value;
                        Debug.WriteLine($"Found modification_type: {target["modification_type"]}");
                    }
                    else
                    {
                        target["modification_type"] = "static"; // Default
                        Debug.WriteLine("Using default modification_type: static");
                    }
                    
                    // Try to extract status_code
                    var statusMatch = StatusCodeRegex.Match(itemJson);
                    if (statusMatch.Success)
                    {
                        target["status_code"] = int.Parse(statusMatch.Groups[1].Value);
                        Debug.WriteLine($"Found status_code: {target["status_code"]}");
                    }
                    
                    // Check for null status code (appears as "status_code":null)
                    var nullStatusMatch = NullStatusCodeRegex.Match(itemJson);
                    if (nullStatusMatch.Success)
                    {
                        target["status_code"] = DBNull.Value; // Use DBNull instead of null
                        Debug.WriteLine("Found null status_code");
                    }
                    
                    // Try to extract target_status_code
                    var targetStatusMatch = TargetStatusCodeRegex.Match(itemJson);
                    if (targetStatusMatch.Success)
                    {
                        target["target_status_code"] = int.Parse(targetStatusMatch.Groups[1].Value);
                        Debug.WriteLine($"Found target_status_code: {target["target_status_code"]}");
                    }
                    
                    // Check for null target status code
                    var nullTargetStatusMatch = NullTargetStatusCodeRegex.Match(itemJson);
                    if (nullTargetStatusMatch.Success)
                    {
                        target["target_status_code"] = DBNull.Value; // Use DBNull instead of null
                        Debug.WriteLine("Found null target_status_code");
                    }
                    
                    // Try to extract dynamic_code (just check if it exists, we don't need the content for the list view)
                    var dynamicCodeMatch = DynamicCodeRegex.Match(itemJson);
                    if (dynamicCodeMatch.Success && target["modification_type"].ToString() == "dynamic")
                    {
                        target["dynamic_code"] = "Dynamic code exists";
                        Debug.WriteLine("Found dynamic_code");
                    }
                    
                    // Try to extract static_response (just check if it exists, we don't need the content for the list view)
                    var staticResponseMatch = StaticResponseRegex.Match(itemJson);
                    if (staticResponseMatch.Success && target["modification_type"].ToString() == "static")
                    {
                        target["static_response"] = "Static response exists";
                        Debug.WriteLine("Found static_response");
                    }
                    
                    // Try to extract is_enabled
                    var enabledMatch = IsEnabledRegex.Match(itemJson);
                    if (enabledMatch.Success)
                    {
                        target["is_enabled"] = int.Parse(enabledMatch.Groups[1].Value);
                        Debug.WriteLine($"Found is_enabled: {target["is_enabled"]}");
                    }
                    else
                    {
                        target["is_enabled"] = 1; // Default to enabled
                        Debug.WriteLine("Using default is_enabled: 1");
                    }
                    
                    // Add the target to our result
                    result.Add(target);
                    Debug.WriteLine($"Added target to results. Total: {result.Count}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in manual JSON parsing: {ex.Message}");
                Debug.WriteLine($"Exception details: {ex}");
                return [];
            }
        }

        // Update the hardcoded paths to be more flexible and include validation
        private static string ResolvePath(string path)
        {
            try
            {
                // If the path is absolute, use it
                if (Path.IsPathRooted(path))
                {
                    return path;
                }

                // Get the executable directory and use the tools subfolder
                string appDirectory = PythonService.GetAppFolder();
                string toolsPath = Path.Combine(appDirectory, "tools");
                string fullPath = Path.Combine(toolsPath, path);
                Debug.WriteLine($"Resolved path {path} to {fullPath}");
                return fullPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving path: {ex.Message}");
                return path;
            }
        }

        // Update the python executable path to use the embedded version
        private string GetPythonExecutablePath()
        {
            return PythonService.GetPythonExecutablePath();
        }

        // Model class for target items
        public class TargetItem
        {
            public int Id { get; set; }
            public string TargetUrl { get; set; } = string.Empty;
            public string? HttpStatus { get; set; } = "200";
            public string? TargetHttpStatus { get; set; }
            public bool IsStaticResponse { get; set; } = true;
            public bool IsNoModification { get; set; } = false;
            public string ModificationType { get; set; } = "static";
            public string ResponseContent { get; set; } = string.Empty;
            public bool IsEnabled { get; set; } = true;
        }
    }
} 