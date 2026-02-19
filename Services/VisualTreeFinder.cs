namespace Launchbox.Services;

public class VisualTreeFinder
{
    private readonly IVisualTreeService _visualTreeService;

    public VisualTreeFinder(IVisualTreeService visualTreeService)
    {
        _visualTreeService = visualTreeService;
    }

    public T? FindFirstDescendant<T>(object root) where T : class
    {
        if (root == null)
        {
            return null;
        }

        int count = _visualTreeService.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = _visualTreeService.GetChild(root, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var result = FindFirstDescendant<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
