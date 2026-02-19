using System;
using System.IO;

namespace Launchbox.Helpers;

public static class PathSecurity
{
    public static bool IsUnsafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Check for NT object path prefix (\??\) which can bypass UNC checks
        if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for specific UNC patterns
        if (path.StartsWith(@"\\?\UNC", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
        if (path.StartsWith(@"\\") || path.StartsWith("//"))
        {
            return true;
        }

        // Check for mixed slash UNC paths (e.g. /\server/share or \/server/share)
        if (path.StartsWith(@"/\") || path.StartsWith(@"\/"))
        {
            return true;
        }

        // Check normalized path for hidden UNC (Defense in depth)
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\") || fullPath.StartsWith("//"))
            {
                return true;
            }

            if (new Uri(fullPath).IsUnc)
            {
                return true;
            }
        }
        catch
        {
        }

        // Check using Uri as a backup
        try
        {
            if (new Uri(path).IsUnc)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
