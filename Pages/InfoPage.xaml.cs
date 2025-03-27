using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;
using Windows.ApplicationModel;

namespace WinUI_V3.Pages
{
    public sealed partial class InfoPage : Page
    {
        public InfoPage()
        {
            this.InitializeComponent();
            
            // Set version information
            LoadVersionInfo();
        }
        
        private void LoadVersionInfo()
        {
            try
            {
                // Get the app version
                Package package = Package.Current;
                PackageId packageId = package.Id;
                PackageVersion version = packageId.Version;
                
                string versionString = $"Version {version.Major}.{version.Minor}.{version.Build}";
                
                // Update the version text
                VersionText.Text = versionString;
            }
            catch (Exception)
            {
                // Fallback to assembly version if package info isn't available
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version;
                string versionString = $"Version {assemblyVersion?.Major ?? 1}.{assemblyVersion?.Minor ?? 0}.{assemblyVersion?.Build ?? 0}";
                
                VersionText.Text = versionString;
            }
        }
    }
} 