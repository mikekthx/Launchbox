using System;

namespace Launchbox.Services;

public record IconCacheEntry(string? SelectedPath, DateTime PngTime, DateTime IcoTime);
