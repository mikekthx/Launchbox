using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Launchbox.Services;

public class VisualTreeHelperWrapper : IVisualTreeHelperWrapper
{
    public int GetChildrenCount(DependencyObject reference)
    {
        return VisualTreeHelper.GetChildrenCount(reference);
    }

    public DependencyObject GetChild(DependencyObject reference, int childIndex)
    {
        return VisualTreeHelper.GetChild(reference, childIndex);
    }
}
