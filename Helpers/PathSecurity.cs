using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Launchbox.Helpers;

public static class PathSecurity
{
    // Security: regex to strip only known-safe web URI schemes from argument strings
    // so that remaining "//" can be detected as UNC path indicators.
    // IMPORTANT: Only safe schemes are stripped. Network-file schemes (file://, smb://, nfs://, dav://)
    // are intentionally NOT stripped so their "//" is caught as a UNC indicator.
    private static readonly Regex SAFE_URI_SCHEME_PATTERN = new(@"https?://|ftp://", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Cached arrays to avoid per-call allocations in hot validation paths
    private static readonly char[] INVALID_PATH_CHARS = Path.GetInvalidPathChars();
    private static readonly char[] SPECIAL_INVALID_CHARS = ['|', '<', '>', '"', '*'];
    private static readonly char[] PATH_SEPARATORS = ['\\', '/'];

    /// <summary>
    /// Detects UNC path patterns in argument strings while allowing legitimate URLs.
    /// Blocks: \\server\share, //server/share, \/server, /\server
    /// Allows: https://example.com, http://example.com, --url=https://example.com
    /// </summary>
    public static bool ContainsUncPath(string? args)
    {
        if (string.IsNullOrEmpty(args))
        {
            return false;
        }

        // Check for backslash-based UNC patterns: \\, \/, /\
        // These never appear in legitimate URLs, so simple substring check is safe.
        if (args.Contains(@"\\") || args.Contains(@"\/") || args.Contains(@"/\"))
        {
            return true;
        }

        // For //, we need to distinguish UNC paths (//server/share) from URLs (https://example.com).
        // A legitimate URL has :// where the colon is part of the scheme. Strip those out and check
        // if any remaining // exists.
        string withoutSchemes = SAFE_URI_SCHEME_PATTERN.Replace(args, "SCHEME_REMOVED");
        return withoutSchemes.Contains("//");
    }

    public static bool IsUnsafePath(string? path)
    {
        // Security: Process.Start("") with UseShellExecute opens the working directory in Explorer
        if (string.IsNullOrWhiteSpace(path)) return true;

        // Check for invalid path characters (Fail Closed)
        // Explicitly check for common Windows invalid characters that might pass Path.GetFullPath on some runtimes
        if (path.IndexOfAny(INVALID_PATH_CHARS) >= 0) return true;
        if (path.IndexOfAny(SPECIAL_INVALID_CHARS) >= 0) return true;

        // Check for '?' separately to allow the valid extended path prefix \\?\
        int qIndex = path.IndexOf('?');
        if (qIndex >= 0)
        {
            const int expectedQuestionMarkIndex = 2;
            const int startSearchIndexForExtraQuestionMarks = 3;

            // If ? is present, it must be the 3rd char (index 2) AND path must start with \\?\
            // Also ensure there are no *other* question marks later in the path
            if (qIndex != expectedQuestionMarkIndex ||
                !path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                (path.Length > startSearchIndexForExtraQuestionMarks && path.IndexOf('?', startSearchIndexForExtraQuestionMarks) >= 0))
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
            // Block anything else starting with \\?\ (e.g. GLOBALROOT, Volume, etc.)
            // Must be at least 7 chars: \\?\C:\
            if (path.Length < 7 || !char.IsLetter(path[4]) || path[5] != ':' || path[6] != '\\')
            {
                return true;
            }

            return false;
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

            // String manipulation is used directly rather than normalizing separators first
            // to avoid introducing new path traversal vectors via the normalization step itself.
            int lastSlash = path.LastIndexOfAny(PATH_SEPARATORS);
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

    // Matches single-quoted strings in exception messages (e.g., 'C:\Users\file.txt')
    private static readonly Regex QUOTED_PATH_PATTERN = new(@"'([^']+[\\/][^']+)'", RegexOptions.Compiled);

    // Matches unquoted Windows drive paths (e.g., C:\Users\file.txt) and UNC paths (\\server\share)
    private static readonly Regex UNQUOTED_PATH_PATTERN = new(@"(?:[A-Za-z]:\\|\\\\)[^\s""']+", RegexOptions.Compiled);

    /// <summary>
    /// Returns a safe exception message that preserves diagnostic context while redacting
    /// sensitive file paths. Both quoted and unquoted paths are redacted via <see cref="RedactPath"/>.
    /// </summary>
    public static string GetSafeExceptionMessage(Exception ex)
    {
        if (ex == null) return "[Unknown Error]";

        string typeName = ex.GetType().Name;
        string message = ex.Message;

        if (string.IsNullOrEmpty(message))
            return $"[{typeName}]";

        // Pass 1: redact quoted paths (e.g., 'C:\Secret\file.txt')
        string safeMessage = QUOTED_PATH_PATTERN.Replace(message, m =>
            $"'{RedactPath(m.Groups[1].Value)}'");

        // Pass 2: redact remaining unquoted Windows/UNC paths
        safeMessage = UNQUOTED_PATH_PATTERN.Replace(safeMessage, m =>
            RedactPath(m.Value));

        return $"[{typeName}] {safeMessage}";
    }
}
