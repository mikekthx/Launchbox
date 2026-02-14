using System;
using System.Threading.Tasks;

namespace Launchbox.Services;

public interface IFilePickerService
{
    Task<string?> PickSingleFolderAsync(object window);
}
