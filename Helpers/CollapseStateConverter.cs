using Microsoft.UI.Xaml.Data;
using System;

namespace Launchbox.Helpers;

public class CollapseStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string _) =>
        value is true ? Localization.GetString("CollapseState_Collapsed") : Localization.GetString("CollapseState_Expanded");

    public object ConvertBack(object value, Type targetType, object parameter, string _) =>
        throw new NotSupportedException();
}
