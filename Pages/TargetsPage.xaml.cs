using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace WinUI_V3.Pages
{
    public sealed partial class TargetsPage : Page
    {
        // Observable collection to store and display targets
        private ObservableCollection<TargetItem> Targets { get; } = new ObservableCollection<TargetItem>();
        
        // Track if we're editing an existing item
        private TargetItem? CurrentEditingItem { get; set; }

        public TargetsPage()
        {
            this.InitializeComponent();
            
            // Set the data context for the list view
            TargetsListView.ItemsSource = Targets;
        }

        private async void AddTargetButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset dialog fields
            TargetUrlTextBox.Text = string.Empty;
            HttpStatusComboBox.SelectedIndex = 0; // Default to 200 OK
            StaticResponseRadioButton.IsChecked = true;
            StaticResponseTextBox.Text = string.Empty;
            DynamicResponseTextBox.Text = @"def handle_wemod_response(flow: http.HTTPFlow) -> None:
    content_type = flow.response.headers.get(""Content-Type"", """").lower()
    if ""application/json"" not in content_type:
        return
    try:
        response_data = json.loads(flow.response.content)
        #Temp
        new_content = json.dumps(response_data).encode(""utf-8"")
        flow.response.content = new_content
        flow.response.headers[""Content-Length""] = str(len(new_content))
        flow.response.headers[""Content-Type""] = content_type";
            
            CurrentEditingItem = null;
            
            // Show the dialog
            await AddTargetDialog.ShowAsync();
        }

        private async void EditTargetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TargetItem targetItem)
            {
                // Populate dialog with existing values
                TargetUrlTextBox.Text = targetItem.TargetUrl;
                
                // Find and select the matching HTTP status
                foreach (ComboBoxItem item in HttpStatusComboBox.Items)
                {
                    if (item.Tag.ToString() == targetItem.HttpStatus)
                    {
                        HttpStatusComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Set response type
                if (targetItem.IsStaticResponse)
                {
                    StaticResponseRadioButton.IsChecked = true;
                    StaticResponseTextBox.Text = targetItem.ResponseContent;
                }
                else
                {
                    DynamicResponseRadioButton.IsChecked = true;
                    DynamicResponseTextBox.Text = targetItem.ResponseContent;
                }
                
                CurrentEditingItem = targetItem;
                
                // Show the dialog
                await AddTargetDialog.ShowAsync();
            }
        }

        private void DeleteTargetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TargetItem targetItem)
            {
                Targets.Remove(targetItem);
            }
        }

        private void ResponseTypeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StaticResponseRadioButton.IsChecked == true)
            {
                StaticResponsePanel.Visibility = Visibility.Visible;
                DynamicResponsePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                StaticResponsePanel.Visibility = Visibility.Collapsed;
                DynamicResponsePanel.Visibility = Visibility.Visible;
            }
        }

        private void AddTargetDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Get values from the dialog
            string targetUrl = TargetUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUrl))
            {
                args.Cancel = true;
                return;
            }
            
            string httpStatus = (HttpStatusComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "200";
            bool isStaticResponse = StaticResponseRadioButton.IsChecked == true;
            string responseContent = isStaticResponse ? 
                StaticResponseTextBox.Text : 
                DynamicResponseTextBox.Text;
            
            if (CurrentEditingItem != null)
            {
                // Update existing item
                CurrentEditingItem.TargetUrl = targetUrl;
                CurrentEditingItem.HttpStatus = httpStatus;
                CurrentEditingItem.IsStaticResponse = isStaticResponse;
                CurrentEditingItem.ResponseContent = responseContent;
            }
            else
            {
                // Create new item
                Targets.Add(new TargetItem
                {
                    TargetUrl = targetUrl,
                    HttpStatus = httpStatus,
                    IsStaticResponse = isStaticResponse,
                    ResponseContent = responseContent
                });
            }
        }
    }

    // Model class for target items
    public class TargetItem
    {
        public string TargetUrl { get; set; } = string.Empty;
        public string HttpStatus { get; set; } = "200";
        public bool IsStaticResponse { get; set; } = true;
        public string ResponseContent { get; set; } = string.Empty;
    }
} 