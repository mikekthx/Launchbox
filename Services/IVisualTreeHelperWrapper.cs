using Microsoft.UI.Xaml;

namespace Launchbox.Services;

public interface IVisualTreeHelperWrapper
{
    int GetChildrenCount(DependencyObject reference);
    DependencyObject GetChild(DependencyObject reference, int childIndex);
}
