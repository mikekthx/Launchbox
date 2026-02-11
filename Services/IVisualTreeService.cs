namespace Launchbox.Services;

public interface IVisualTreeService
{
    int GetChildrenCount(object parent);
    object GetChild(object parent, int index);
}
