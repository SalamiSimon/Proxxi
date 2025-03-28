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
using WinUI_V3.Helpers;

namespace WinUI_V3.Pages
{
    public sealed partial class TargetsPage : Page
    {
        // Observable collection to store and display targets
        private ObservableCollection<TargetItem> Targets { get; } = new ObservableCollection<TargetItem>();
        
        // Track if we're editing an existing item
        private TargetItem? CurrentEditingItem { get; set; }
        
        // Flag to check if Python environment is available
        private bool IsPythonEnvironmentAvailable { get; set; } = true;
        
        // HARDCODED PATHS - TEMPORARY
        private readonly string DbPath = @"C:\Users\Sten\Desktop\PROXIMITM\targets.db";
        private readonly string ModularPath = @"C:\Users\Sten\Desktop\PROXIMITM\mitm_modular";
        private readonly string SamplesPath = @"C:\Users\Sten\Desktop\PROXIMITM\mitm_modular\samples";

        public TargetsPage()
        {
            try
            {
                this.InitializeComponent();
                
                // Register for Loaded event to ensure UI elements are fully initialized
                this.Loaded += TargetsPage_Loaded;
                
                // Set the data context for the list view
                TargetsListView.ItemsSource = Targets;
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash
                Debug.WriteLine($"Error initializing TargetsPage: {ex.Message}");
                ShowErrorMessage("Initialization Error", $"Failed to initialize the page: {ex.Message}");
            }
        }

        private void TargetsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load targets when the page is fully loaded
                LoadTargets();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TargetsPage_Loaded: {ex.Message}");
            }
        }

        // Load targets from the database
        private async void LoadTargets()
        {
            try
            {
                // Check if Python is available before attempting to use it
                if (!await IsPythonAvailable())
                {
                    IsPythonEnvironmentAvailable = false;
                    ShowErrorMessage(
                        "Python Not Found", 
                        "Python is required for the targets functionality. Please ensure Python is installed and available in your PATH."
                    );
                    return;
                }
                
                // Clear existing items
                Targets.Clear();
                
                // If the database file doesn't exist, create an empty one or show a message
                if (!File.Exists(DbPath))
                {
                    // Add a sample item for demonstration
                    Targets.Add(new TargetItem
                    {
                        Id = 0,
                        TargetUrl = "No targets found. Database file not found at: " + DbPath,
                        HttpStatus = "Any",
                        IsStaticResponse = true,
                        ResponseContent = "{}",
                        IsEnabled = true
                    });
                    return;
                }
                
                // Run the CLI command to list targets with JSON output - no db argument needed
                var results = await RunCliCommandAsync("json-list-all");
                
                // For each target returned by the CLI, add it to our observable collection
                foreach (var target in results)
                {
                    Targets.Add(target);
                }
                
                // If no targets were found, add a placeholder
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
            }
            catch (Exception ex)
            {
                // Log the error but don't crash
                Debug.WriteLine($"Error loading targets: {ex.Message}");
                ShowErrorMessage("Error Loading Targets", $"Failed to load targets: {ex.Message}");
                
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
            }
        }
        
        // Check if Python is available
        private async Task<bool> IsPythonAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
        
        // Show an error message dialog
        private async void ShowErrorMessage(string title, string message)
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
                // If we can't even show the dialog, log to debug output
                Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
                Debug.WriteLine($"Original error: {title} - {message}");
            }
        }

        // Run the mitm_modular CLI command and return the results
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
                // Create process start info
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    WorkingDirectory = Path.GetDirectoryName(ModularPath), // Set working directory to parent of the mitm_modular directory
                    Arguments = $"-m mitm_modular.cli {command} {string.Join(" ", args)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                Debug.WriteLine($"Running command: {startInfo.FileName} {startInfo.Arguments}");
                Debug.WriteLine($"Working directory: {startInfo.WorkingDirectory}");
                
                // Start the process
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    Debug.WriteLine($"Command output: {output}");
                    if (!string.IsNullOrEmpty(errors)) {
                        Debug.WriteLine($"Command errors: {errors}");
                    }
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Error executing CLI command: {errors}");
                    }
                    
                    // Add this right after reading the CLI output
                    Debug.WriteLine("Full CLI output:");
                    Debug.WriteLine(output);
                    Debug.WriteLine("------ End of CLI output ------");
                    
                    // Handle json-list and json-list-all commands
                    if ((command == "json-list" || command == "json-list-all") && output.Trim().StartsWith("[") && output.Trim().EndsWith("]"))
                    {
                        try 
                        {
                            // Deserialize JSON directly
                            var jsonTargets = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(output);

                            foreach (var target in jsonTargets)
                            {
                                var targetItem = new TargetItem
                                {
                                    Id = target["id"].GetInt32(),
                                    TargetUrl = target["url"].GetString(),
                                    HttpStatus = target["status_code"].ValueKind == JsonValueKind.Null ? null : target["status_code"].GetInt32().ToString(),
                                    TargetHttpStatus = target["target_status_code"].ValueKind == JsonValueKind.Null ? null : target["target_status_code"].GetInt32().ToString(),
                                    IsStaticResponse = target["modification_type"].GetString() == "static",
                                    ResponseContent = target["modification_type"].GetString() == "dynamic" 
                                        ? target["dynamic_code"].GetString() 
                                        : target["static_response"].GetString(),
                                    IsEnabled = target["is_enabled"].GetInt32() == 1
                                };

                                results.Add(targetItem);
                            }
                        }
                        catch (Exception ex) 
                        {
                            Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                            throw new Exception($"Error parsing JSON: {ex.Message}", ex);
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
                                var parts = output.Split(new[] { "Dynamic Code:", "-------------" }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 2)
                                {
                                    content = parts[2].Trim();
                                }
                            }
                            else if (type == "static" && output.Contains("Static Response:"))
                            {
                                var parts = output.Split(new[] { "Static Response:", "---------------" }, StringSplitOptions.RemoveEmptyEntries);
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
                            foreach (ComboBoxItem item in HttpStatusComboBox.Items)
                            {
                                if (item.Tag.ToString() == targetItem.HttpStatus)
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
                            foreach (ComboBoxItem item in TargetHttpStatusComboBox.Items)
                            {
                                if (item.Tag.ToString() == targetItem.TargetHttpStatus)
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
                    ContentDialog confirmDialog = new ContentDialog
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
                    
                    // Run the CLI command to delete the target
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
                    
                    // Run the CLI command to enable/disable the target
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
                if (NoModificationRadioButton?.IsChecked == true && StaticResponsePanel != null && DynamicResponsePanel != null)
                {
                    StaticResponsePanel.Visibility = Visibility.Collapsed;
                    DynamicResponsePanel.Visibility = Visibility.Collapsed;
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
                string samplePath = Path.Combine(SamplesPath, "static.json");
                
                if (File.Exists(samplePath))
                {
                    string sampleContent = await File.ReadAllTextAsync(samplePath);
                    if (StaticSampleTextBox != null)
                        StaticSampleTextBox.Text = sampleContent;
                    
                    if (StaticSampleDialog != null)
                        await StaticSampleDialog.ShowAsync();
                }
                else
                {
                    ShowErrorMessage("Sample Not Found", $"Could not find the sample file at: {samplePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing static sample: {ex.Message}");
                ShowErrorMessage("Error", $"Failed to show static sample: {ex.Message}");
            }
        }
        
        private async void DynamicSampleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string samplePath = Path.Combine(SamplesPath, "dynamic.py");
                
                if (File.Exists(samplePath))
                {
                    string sampleContent = await File.ReadAllTextAsync(samplePath);
                    if (DynamicSampleTextBox != null)
                        DynamicSampleTextBox.Text = sampleContent;
                    
                    if (DynamicSampleDialog != null)
                        await DynamicSampleDialog.ShowAsync();
                }
                else
                {
                    ShowErrorMessage("Sample Not Found", $"Could not find the sample file at: {samplePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing dynamic sample: {ex.Message}");
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
                    args.Cancel = true;
                    return;
                }
                
                // Get match HTTP status and target HTTP status
                string httpStatus = "Any";
                if (HttpStatusComboBox?.SelectedItem is ComboBoxItem httpStatusItem && httpStatusItem.Tag != null)
                {
                    httpStatus = httpStatusItem.Tag.ToString();
                }
                
                string targetHttpStatus = "NoChange";
                if (TargetHttpStatusComboBox?.SelectedItem is ComboBoxItem targetStatusItem && targetStatusItem.Tag != null)
                {
                    targetHttpStatus = targetStatusItem.Tag.ToString();
                }
                
                // Determine the response type
                bool isNoModification = NoModificationRadioButton?.IsChecked == true;
                bool isStaticResponse = StaticResponseRadioButton?.IsChecked == true;
                
                string responseContent = string.Empty;
                
                if (!isNoModification)
                {
                    if (isStaticResponse && StaticResponseTextBox != null)
                    {
                        responseContent = StaticResponseTextBox.Text;
                    }
                    else if (DynamicResponseTextBox != null)
                    {
                        responseContent = DynamicResponseTextBox.Text;
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
                        ShowErrorMessage("Invalid JSON", $"The static response contains invalid JSON: {jsonEx.Message}");
                        args.Cancel = true;
                        return;
                    }
                }
                
                if (CurrentEditingItem != null && CurrentEditingItem.Id != 0)
                {
                    // Delete the existing target and add a new one (since there's no direct edit function in CLI)
                    await RunCliCommandAsync("delete", CurrentEditingItem.Id.ToString());
                }
                
                // Skip adding if it's a no modification target (since the CLI doesn't support this yet)
                if (!isNoModification)
                {
                    // Prepare the arguments for the add command
                    var args_list = new List<string> {
                        "add",
                        $"\"{targetUrl}\"",
                        "--type", isStaticResponse ? "static" : "dynamic"
                    };
                    
                    // Add the status code if not "Any"
                    if (httpStatus != "Any")
                    {
                        args_list.Add("--status");
                        args_list.Add(httpStatus);
                    }
                    
                    // Add the target status code if not "NoChange"
                    if (targetHttpStatus != "NoChange")
                    {
                        args_list.Add("--target-status");
                        args_list.Add(targetHttpStatus);
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
                    await RunCliCommandAsync(args_list[0], args_list.Skip(1).ToArray());
                }
                
                // Reload the targets
                LoadTargets();
                
                // If we had a placeholder, remove it
                var placeholder = Targets.FirstOrDefault(t => t.Id == 0);
                if (placeholder != null)
                {
                    Targets.Remove(placeholder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving target: {ex.Message}");
                ShowErrorMessage("Error Saving Target", $"Failed to save target: {ex.Message}");
                args.Cancel = true;
            }
        }
    }

    // Model class for target items
    public class TargetItem
    {
        public int Id { get; set; }
        public string TargetUrl { get; set; } = string.Empty;
        public string? HttpStatus { get; set; } = "200";
        public string? TargetHttpStatus { get; set; }
        public bool IsStaticResponse { get; set; } = true;
        public string ResponseContent { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
} 