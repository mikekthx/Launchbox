using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Launchbox.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value is bool b && b;

        if (parameter is string paramString && paramString.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
