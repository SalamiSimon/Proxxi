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
using WinUI_V3.Pages;

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
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 800, Height = 600 });

            // Set up navigation
            NavView.SelectionChanged += NavView_SelectionChanged;
            
            // Navigate to the first page and select the first item
            ContentFrame.Navigate(typeof(TargetsPage));
            NavView.SelectedItem = NavView.MenuItems[0]; // Select the first item by default
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
    }
}
