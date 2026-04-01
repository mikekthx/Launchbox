using System.Text.Json.Serialization;

namespace Launchbox.Models;

public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required int Order { get; init; }

    private readonly string? _cachedExpandedPath;

    // Pre-populated by ValidateAndNormalize to avoid repeated P/Invoke in hot paths.
    // Falls back to on-demand expansion so deserialized instances always return a usable path.
    [JsonIgnore]
    public string ExpandedPath
    {
        get => _cachedExpandedPath ?? Environment.ExpandEnvironmentVariables(Path);
        init => _cachedExpandedPath = value;
    }
}
