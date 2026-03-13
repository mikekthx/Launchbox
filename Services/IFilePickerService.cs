using System;
using System.Threading.Tasks;

namespace Launchbox.Services;

public interface IFilePickerService
{
    object? OwnerWindow { get; set; }
    Task<string?> PickSingleFolderAsync();
}
