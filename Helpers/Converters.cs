using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace WinUI_V3.Helpers
{
    /// <summary>
    /// Converts a boolean value to a string based on a parameter of format "TrueValue|FalseValue"
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                if (options.Length == 2)
                {
                    return boolValue ? options[0] : options[1];
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean value to a brush color based on a parameter of format "TrueColor|FalseColor"
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                if (options.Length == 2)
                {
                    string colorName = boolValue ? options[0] : options[1];
                    
                    // Known colors
                    switch (colorName.ToLowerInvariant())
                    {
                        case "red":
                            return new SolidColorBrush(Colors.Red);
                        case "green":
                            return new SolidColorBrush(Colors.Green);
                        case "blue":
                            return new SolidColorBrush(Colors.Blue);
                        case "yellow":
                            return new SolidColorBrush(Colors.Yellow);
                        case "orange":
                            return new SolidColorBrush(Colors.Orange);
                        case "purple":
                            return new SolidColorBrush(Colors.Purple);
                        case "gray":
                        case "grey":
                            return new SolidColorBrush(Colors.Gray);
                        case "black":
                            return new SolidColorBrush(Colors.Black);
                        case "white":
                            return new SolidColorBrush(Colors.White);
                    }
                }
            }
            
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Converts a boolean value to a Style resource based on a parameter of format "TrueStyleKey|FalseStyleKey"
    /// </summary>
    public class BoolToStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                if (options.Length == 2)
                {
                    string styleKey = boolValue ? options[0] : options[1];
                    
                    // Try to find the style in application resources
                    if (Application.Current.Resources.TryGetValue(styleKey, out object style))
                    {
                        return style;
                    }
                }
            }
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
} 