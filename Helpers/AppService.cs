using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace WinUI_V3.Helpers
{
    public static class AppService
    {
        private static bool _isInitialized = false;
        private static bool _isFirstLaunch = true;
        
        // First launch file marker
        private static readonly string FirstLaunchMarkerPath = Path.Combine(
            PythonService.GetAppFolder(), 
            ".app_initialized");
        
        // Call this method when the app starts to perform initialization tasks
        public static async Task InitializeAppAsync()
        {
            if (_isInitialized)
                return;
                
            try
            {
                Debug.WriteLine("Initializing application services...");
                
                // Check if the app has been launched before
                _isFirstLaunch = !File.Exists(FirstLaunchMarkerPath);
                Debug.WriteLine($"Is first launch: {_isFirstLaunch}");
                
                // Check proxy status regardless of first launch
                await ProxyService.InitializeOnStartup();
                
                // If this is the first launch, perform additional setup
                if (_isFirstLaunch)
                {
                    // Create the first launch marker file
                    try
                    {
                        File.WriteAllText(FirstLaunchMarkerPath, DateTime.Now.ToString());
                        Debug.WriteLine("Created first launch marker file");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to create first launch marker file: {ex.Message}");
                    }
                }
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing app services: {ex.Message}");
            }
        }
        
        // Check if this is the app's first launch
        public static bool IsFirstLaunch()
        {
            return _isFirstLaunch;
        }
        
        // Check if dependencies should be verified
        public static async Task<bool> ShouldCheckDependencies()
        {
            // Always check dependencies on first launch
            if (_isFirstLaunch)
                return true;
                
            // For subsequent launches, check if Python and mitmproxy are still available
            // This helps detect if they've been uninstalled since the last run
            return !(await DependencyService.CheckDependencies());
        }
    }
} 