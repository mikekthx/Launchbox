using System;
using System.Text.Json.Serialization;

namespace Launchbox.Models;

public record ShortcutFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required int Order { get; init; }

    private readonly string? _cachedExpandedPath;

    /// <summary>
    /// The expanded form of <see cref="Path"/>, with environment variables resolved.
    /// Pre-populated by <c>ValidateAndNormalize</c> for efficiency; falls back to on-demand
    /// expansion for instances that bypass that path (e.g. deserialized from the settings store).
    /// Callers that construct a <see cref="ShortcutFolder"/> outside of <c>ValidateAndNormalize</c>
    /// are responsible for validating the result (e.g. via <see cref="Helpers.PathSecurity.IsUnsafePath"/>).
    /// </summary>
    [JsonIgnore]
    public string ExpandedPath
    {
        get => _cachedExpandedPath ?? Environment.ExpandEnvironmentVariables(Path);
        init => _cachedExpandedPath = value;
    }

    // Exclude _cachedExpandedPath from record equality — two folders are the same if their
    // Path/Label/Order match, regardless of whether the expansion has been cached.
    public virtual bool Equals(ShortcutFolder? other) =>
        other is not null &&
        EqualityContract == other.EqualityContract &&
        Path == other.Path &&
        Label == other.Label &&
        Order == other.Order;

    public override int GetHashCode() => HashCode.Combine(EqualityContract, Path, Label, Order);
}
