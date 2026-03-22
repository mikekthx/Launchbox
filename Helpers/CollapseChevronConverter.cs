using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

public class CollapseChevronConverter : IValueConverter
{
    // ChevronRight (collapsed) vs ChevronDown (expanded) — standard Windows tree-view convention
    public object Convert(object value, Type targetType, object parameter, string _) =>
        value is true ? "\uE76C" : "\uE76E";

    public object ConvertBack(object value, Type targetType, object parameter, string _) =>
        throw new NotSupportedException();
}
