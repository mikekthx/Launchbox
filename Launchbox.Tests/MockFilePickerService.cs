using Launchbox.Services;
using System.Threading.Tasks;

namespace Launchbox.Tests;

public class MockFilePickerService : IFilePickerService
{
    public string? SelectedFolder { get; set; }
    public object? OwnerWindow { get; set; }

    public Task<string?> PickSingleFolderAsync()
    {
        return Task.FromResult(SelectedFolder);
    }

    public string? RenameResult { get; set; }
    public Task<string?> ShowRenameFolderDialogAsync(string currentLabel)
    {
        return Task.FromResult(RenameResult);
    }
}
