namespace Launchbox.Services;

public interface IShortcutService
{
    string[]? GetShortcutFiles(string folderPath, System.Collections.Generic.IReadOnlyList<string> allowedExtensions);
}
