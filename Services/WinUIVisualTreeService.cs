using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Launchbox.Services;

public class WinUIVisualTreeService : IVisualTreeService
{
    private readonly IVisualTreeHelperWrapper _visualTreeHelper;

    public WinUIVisualTreeService(IVisualTreeHelperWrapper? visualTreeHelper = null)
    {
        _visualTreeHelper = visualTreeHelper ?? new VisualTreeHelperWrapper();
    }

    public int GetChildrenCount(object parent)
    {
        if (parent is DependencyObject d)
        {
            return _visualTreeHelper.GetChildrenCount(d);
        }
        return 0;
    }

    public object GetChild(object parent, int index)
    {
        if (parent is DependencyObject d)
        {
            return _visualTreeHelper.GetChild(d, index);
        }
        throw new System.ArgumentException("Parent must be a DependencyObject");
    }
}
