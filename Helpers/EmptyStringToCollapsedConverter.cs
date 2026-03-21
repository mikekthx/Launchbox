using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

/// <summary>
/// Hides collapsed-group placeholder items (empty Name) so they take zero visual space.
/// </summary>
public class EmptyStringToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
