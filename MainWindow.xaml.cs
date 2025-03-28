using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using WinUI_V3.Pages;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUI_V3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Response modifier for Proxifier";
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarArea);

            // Set the window size
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 800, Height = 800 });

            // Set up navigation
            NavView.SelectionChanged += NavView_SelectionChanged;
            
            // Navigate to the first page and select the first item
            ContentFrame.Navigate(typeof(TargetsPage));
            NavView.SelectedItem = NavView.MenuItems[0]; // Select the first item by default
            
            // Handle coffee button image loading errors
            var coffeeButtonContent = CoffeeButton.Content as Grid;
            var image = coffeeButtonContent?.Children.OfType<Image>().FirstOrDefault();
            
            image?.RegisterPropertyChangedCallback(
                Image.SourceProperty, (s, e) => 
                {
                    try
                    {
                        var img = s as Image;
                        if (img != null && img.Source == null)
                        {
                            // Image failed to load, show fallback icon
                            if (FallbackIcon != null)
                                FallbackIcon.Visibility = Visibility.Visible;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error handling coffee icon: {ex.Message}");
                        // Ensure fallback icon is visible
                        if (FallbackIcon != null)
                            FallbackIcon.Visibility = Visibility.Visible;
                    }
                });
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
                return;

            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                var tag = selectedItem.Tag?.ToString();
                switch (tag)
                {
                    case "targets":
                        ContentFrame.Navigate(typeof(TargetsPage));
                        break;
                    case "settings":
                        ContentFrame.Navigate(typeof(SettingsPage));
                        break;
                    case "info":
                        ContentFrame.Navigate(typeof(InfoPage));
                        break;
                }
            }
        }

        private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            // Update layout for closed pane - content now takes full width
            UpdateContentMargins();
        }

        private void NavView_PaneOpening(NavigationView sender, object args)
        {
            // Update layout for open pane
            UpdateContentMargins();
        }

        private void UpdateContentMargins()
        {
            // This method could be used to adjust any additional margins or layout properties
            // if needed, depending on the pane state
            
            // For most cases, WinUI's NavigationView handles this automatically
            // but you can add custom adjustments here if necessary
        }

        private async void CoffeeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowCoffeeDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing coffee dialog: {ex.Message}");
            }
        }
        
        private async Task ShowCoffeeDialog()
        {
            // Create the coffee donation dialog
            var coffeeDialog = new ContentDialog
            {
                Title = "Support me",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonText = "Donate",
                SecondaryButtonText = "Activate License Key",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            
            // Create content for the dialog
            var content = new StackPanel
            {
                Spacing = 16,
                Margin = new Thickness(0, 12, 0, 0)
            };
            
            // Add thank you message
            content.Children.Add(new TextBlock
            {
                Text = "Thank you for considering a donation! Your support helps maintain and improve PROXIMITM.",
                TextWrapping = TextWrapping.Wrap
            });
            
            // Add perks section
            var perksPanel = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 12)
            };
            
            perksPanel.Children.Add(new TextBlock
            {
                Text = "Supporters receive:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            
            var perksList = new StackPanel { Spacing = 4 };
            
            var perks = new string[]
            {
                "Nothing"
            };
            
            foreach (var perk in perks)
            {
                var perkItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };
                
                perkItem.Children.Add(new SymbolIcon
                {
                    Symbol = Symbol.Accept,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green)
                });
                
                perkItem.Children.Add(new TextBlock
                {
                    Text = perk,
                    TextWrapping = TextWrapping.Wrap
                });
                
                perksList.Children.Add(perkItem);
            }
            
            perksPanel.Children.Add(perksList);
            content.Children.Add(perksPanel);
            
            // Set the content
            coffeeDialog.Content = content;
            
            // Show the dialog and get result
            var result = await coffeeDialog.ShowAsync();
            
            // Handle the result
            if (result == ContentDialogResult.Primary)
            {
                // Donate button clicked
                LaunchDonationPage();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Activate License Key button clicked
                await ShowLicenseKeyDialog();
            }
        }
        
        private void LaunchDonationPage()
        {
            try
            {
                // Launch the default browser with the donation URL
                var startInfo = new ProcessStartInfo
                {
                    FileName = "https://buymeacoffee.com/placeholder",
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching donation page: {ex.Message}");
            }
        }
        
        private async Task ShowLicenseKeyDialog()
        {
            try
            {
                // Create the license key input dialog
                var licenseDialog = new ContentDialog
                {
                    Title = "Enter License Key",
                    XamlRoot = this.Content.XamlRoot,
                    PrimaryButtonText = "Activate",
                    CloseButtonText = "Cancel"
                };
                
                // Create content for the dialog
                var content = new StackPanel
                {
                    Spacing = 16,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                
                // Add explanation
                content.Children.Add(new TextBlock
                {
                    Text = "Enter your license key received after donation:",
                    TextWrapping = TextWrapping.Wrap
                });
                
                // Add license key input box
                var licenseKeyBox = new TextBox
                {
                    PlaceholderText = "XXXX-XXXX-XXXX-XXXX",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                
                content.Children.Add(licenseKeyBox);
                
                // Set the content
                licenseDialog.Content = content;
                
                // Show the dialog and get result
                var result = await licenseDialog.ShowAsync();
                
                // Handle the result
                if (result == ContentDialogResult.Primary)
                {
                    string licenseKey = licenseKeyBox.Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(licenseKey))
                    {
                        // In a real app, you would validate the license key here
                        // For now, just pretend it worked
                        await ShowThankYouMessage();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing license key dialog: {ex.Message}");
            }
        }
        
        private async Task ShowThankYouMessage()
        {
            try
            {
                var thankYouDialog = new ContentDialog
                {
                    Title = "Thank You!",
                    Content = "Your license has been activated. Thank you for supporting PROXIMITM!",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                
                await thankYouDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing thank you dialog: {ex.Message}");
            }
        }
    }
}
