using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WinUI_V3.Helpers
{
    public static class DialogService
    {
        // Show an error dialog
        public static async Task ShowErrorDialog(XamlRoot xamlRoot, string title, string message)
        {
            try
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                
                await errorDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show error dialog: {ex.Message}");
            }
        }
        
        // Show an information dialog
        public static async Task ShowInfoDialog(XamlRoot xamlRoot, string title, string message)
        {
            try
            {
                ContentDialog infoDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                
                await infoDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show info dialog: {ex.Message}");
            }
        }
        
        // Show a confirmation dialog
        public static async Task<bool> ShowConfirmationDialog(XamlRoot xamlRoot, string title, string message, 
            string primaryButtonText = "Yes", string closeButtonText = "No")
        {
            try
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = primaryButtonText,
                    CloseButtonText = closeButtonText,
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot
                };
                
                var result = await confirmDialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show confirmation dialog: {ex.Message}");
                return false;
            }
        }
        
        // Show a dialog with primary, secondary, and close buttons
        public static async Task<ContentDialogResult> ShowDialog(XamlRoot xamlRoot, string title, string message,
            string primaryButtonText, string secondaryButtonText, string closeButtonText)
        {
            try
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = primaryButtonText,
                    SecondaryButtonText = secondaryButtonText,
                    CloseButtonText = closeButtonText,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };
                
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show dialog: {ex.Message}");
                return ContentDialogResult.None;
            }
        }
        
        // Show a dialog with custom content
        public static async Task<ContentDialogResult> ShowCustomDialog(XamlRoot xamlRoot, string title,
            UIElement content, string primaryButtonText, string secondaryButtonText = "", string closeButtonText = "Cancel")
        {
            try
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = primaryButtonText,
                    XamlRoot = xamlRoot
                };
                
                if (!string.IsNullOrEmpty(secondaryButtonText))
                {
                    dialog.SecondaryButtonText = secondaryButtonText;
                }
                
                if (!string.IsNullOrEmpty(closeButtonText))
                {
                    dialog.CloseButtonText = closeButtonText;
                }
                
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show custom dialog: {ex.Message}");
                return ContentDialogResult.None;
            }
        }
    }
} 