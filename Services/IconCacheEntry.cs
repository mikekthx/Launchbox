using System;

namespace Launchbox.Services;

/// <summary>Cache key for icon extraction results.</summary>
public record IconCacheEntry
{
    public byte[]? Content { get; init; }

    /// <summary>Last-write time of the shortcut file (.lnk or .url).</summary>
    public DateTime ShortcutTime { get; init; }

    /// <summary>Last-write time of the custom .png icon in the .icons/ directory (DateTime.MinValue if absent).</summary>
    public DateTime PngTime { get; init; }

    /// <summary>Last-write time of the custom .ico icon in the .icons/ directory (DateTime.MinValue if absent).</summary>
    public DateTime IcoTime { get; init; }

    public IconCacheEntry(byte[]? content, DateTime shortcutTime, DateTime pngTime, DateTime icoTime)
    {
        Content = content;
        ShortcutTime = shortcutTime;
        PngTime = pngTime;
        IcoTime = icoTime;
    }
}
