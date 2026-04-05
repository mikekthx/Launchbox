using System;
using System.Text.Json.Serialization;

namespace Launchbox.Models;

public record ShortcutFolder
{
    private string _path = string.Empty;
    private string? _expandedPath;

    public required string Path
    {
        get => _path;
        init
        {
            _path = value;
            // Necessary to prevent a stale cache from being carried through `with`-expressions
            _expandedPath = null;
        }
    }

    public required string Label { get; init; }
    public required int Order { get; init; }

    /// <summary>
    /// The expanded form of <see cref="Path"/>, with environment variables resolved.
    /// Computed on demand from <see cref="Path"/> — always reflects the current path value,
    /// including after <c>with</c>-expressions that change <see cref="Path"/>.
    /// </summary>
    [JsonIgnore]
    public string ExpandedPath => System.Threading.LazyInitializer.EnsureInitialized(ref _expandedPath, () => Environment.ExpandEnvironmentVariables(Path));

    // Custom equality is required to prevent the compiler-synthesized record equality
    // from including the private `_expandedPath` cache field, which would break equality semantics.
    public virtual bool Equals(ShortcutFolder? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Path == other.Path &&
               Label == other.Label &&
               Order == other.Order;
    }

    public override int GetHashCode() => HashCode.Combine(Path, Label, Order);
}
