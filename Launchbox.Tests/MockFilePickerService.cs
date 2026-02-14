using System.Threading.Tasks;
using Launchbox.Services;

namespace Launchbox.Tests;

public class MockFilePickerService : IFilePickerService
{
    public string? SelectedFolder { get; set; }

    public Task<string?> PickSingleFolderAsync(object window)
    {
        return Task.FromResult(SelectedFolder);
    }
}
