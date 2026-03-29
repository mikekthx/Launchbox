using System.Text.Json.Serialization;

namespace Launchbox.Models;

public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required int Order { get; init; }

    [JsonIgnore]
    public string ExpandedPath { get; init; } = string.Empty;
}
