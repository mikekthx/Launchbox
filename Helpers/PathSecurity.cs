using System;
using System.IO;

namespace Launchbox.Helpers;

public static class PathSecurity
{
    public static bool IsUnsafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Check for invalid path characters (Fail Closed)
        // Explicitly check for common Windows invalid characters that might pass Path.GetFullPath on some runtimes
        char[] invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0) return true;
        if (path.IndexOfAny(new[] { '|', '<', '>', '"', '*' }) >= 0) return true;

        // Check for '?' separately to allow the valid extended path prefix \\?\
        int qIndex = path.IndexOf('?');
        if (qIndex >= 0)
        {
            // If ? is present, it must be the 3rd char (index 2) AND path must start with \\?\
            // Also ensure there are no *other* question marks later in the path
            if (qIndex != 2 || !path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.IndexOf('?', 3) >= 0)
            {
                return true;
            }
        }

        // Check for NT object path prefix (\??\) which can bypass UNC checks
        if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase)) return true;

        // Check for specific UNC patterns
        if (path.StartsWith(@"\\?\UNC", StringComparison.OrdinalIgnoreCase)) return true;

        // Allow local long paths ONLY if they are standard drive paths (e.g. \\?\C:\...)
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            // Must be at least 7 chars: \\?\C:\
            if (path.Length >= 7 &&
                char.IsLetter(path[4]) &&
                path[5] == ':' &&
                path[6] == '\\')
            {
                return false;
            }
            return true; // Block anything else starting with \\?\ (e.g. GLOBALROOT, Volume, etc.)
        }

        // Check for standard UNC paths
        if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;

        // Check for mixed slash UNC paths (e.g. /\server/share or \/server/share)
        if (path.StartsWith(@"/\") || path.StartsWith(@"\/")) return true;

        // Check normalized path for hidden UNC (Defense in depth)
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\") || fullPath.StartsWith("//")) return true;
            if (new Uri(fullPath).IsUnc) return true;
        }
        catch
        {
            // If we can't parse the path, assume it's unsafe (Fail Closed)
            return true;
        }

        // Check using Uri as a backup
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.IsUnc)
            {
                return true;
            }
        }
        catch
        {
            // If unexpected exception occurs, assume unsafe
            return true;
        }

        return false;
    }

    public static string RedactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "[Empty]";

        try
        {
            // On Linux/Mac, Path.GetFileName doesn't understand Windows backslashes.
            // We manually split by backslash if running on non-Windows (or just always for safety)
            // to ensure we get just the filename.

            // Normalize separators first? No, string manipulation is safer.
            int lastSlash = path.LastIndexOfAny(new[] { '\\', '/' });
            string fileName;

            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                fileName = path.Substring(lastSlash + 1);
            }
            else if (lastSlash == path.Length - 1)
            {
                // Ends with slash
                return "[Redacted]";
            }
            else
            {
                // No slashes
                fileName = path;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                // This happens for root paths like C:\ or \\server\share
                return "[Redacted]";
            }
            return $"...\\{fileName}";
        }
        catch
        {
            return "[Invalid Path]";
        }
    }

    /// <summary>
    /// Returns a safe exception message that avoids leaking sensitive paths or system information.
    /// Use this instead of ex.Message when logging exceptions related to file system operations.
    /// </summary>
    public static string GetSafeExceptionMessage(Exception ex)
    {
        if (ex == null) return "[Unknown Error]";

        // Return only the exception type name
        // ex.Message often contains the full path which we want to avoid leaking
        return $"[{ex.GetType().Name}]";
    }
}
