using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Launchbox;

public class WinUIVisualTreeService : IVisualTreeService
{
    public int GetChildrenCount(object parent)
    {
        if (parent is DependencyObject d)
        {
            return VisualTreeHelper.GetChildrenCount(d);
        }
        return 0;
    }

    public object GetChild(object parent, int index)
    {
        if (parent is DependencyObject d)
        {
            return VisualTreeHelper.GetChild(d, index);
        }
        throw new System.ArgumentException("Parent must be a DependencyObject");
    }
}
