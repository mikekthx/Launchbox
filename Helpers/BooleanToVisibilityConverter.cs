using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    // x:Bind function binding requires a static method; IValueConverter.Convert cannot be called by name in compiled bindings.
    // x:Bind also does not support optional/default parameters — overloads with explicit signatures are required.
    public static Visibility ConvertBool(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility ConvertBool(bool value, bool invert) => invert ? ConvertBool(!value) : ConvertBool(value);

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
