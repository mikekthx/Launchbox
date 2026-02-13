using System;

namespace Launchbox.Services;

public record IconCacheEntry(byte[]? Content, DateTime ShortcutTime, DateTime PngTime, DateTime IcoTime);
