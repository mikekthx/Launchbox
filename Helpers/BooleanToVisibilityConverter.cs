using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    // Offloads boolean-to-visibility conversion to a static method to support
    // compiled {x:Bind} function bindings in WinUI 3 Window objects, completely
    // avoiding runtime reflection and StaticResource dictionary lookup overhead.
    public static Visibility ConvertBool(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public object Convert(object value, Type targetType, object parameter, string _)
    {
        bool isVisible = value is bool b && b;

        if (parameter is string paramString && paramString.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string _)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
