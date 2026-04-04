using System;
using System.Text.Json.Serialization;

namespace Launchbox.Models;

public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required int Order { get; init; }

    /// <summary>
    /// The expanded form of <see cref="Path"/>, with environment variables resolved.
    /// Computed on demand from <see cref="Path"/> — always reflects the current path value,
    /// including after <c>with</c>-expressions that change <see cref="Path"/>.
    /// </summary>
    [JsonIgnore]
    public string ExpandedPath => Environment.ExpandEnvironmentVariables(Path);
}
