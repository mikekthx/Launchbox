namespace Launchbox.Services;

public interface IShortcutService
{
    string[]? GetShortcutFiles(string folderPath, string[] allowedExtensions);
}
