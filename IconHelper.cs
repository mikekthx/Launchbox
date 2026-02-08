using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using WinIcon = System.Drawing.Icon;

namespace Launchbox;

public static class IconHelper
{
    public static byte[]? ExtractIconBytes(string path)
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                string iconFile = GetIniValue(path, "InternetShortcut", "IconFile");
                if (File.Exists(iconFile))
                {
                    path = iconFile;
                }
            }

            PrivateExtractIcons(path, 0, 128, 128, ref hIcon, IntPtr.Zero, 1, 0);
            if (hIcon == IntPtr.Zero)
            {
                return null;
            }

            using var icon = WinIcon.FromHandle(hIcon);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract icon for {path}: {ex.Message}");
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }
        }
    }

    private static string GetIniValue(string p, string s, string k)
    {
        var sb = new System.Text.StringBuilder(255);
        GetPrivateProfileString(s, k, string.Empty, sb, 255, p);
        return sb.ToString();
    }

    [DllImport("user32.dll")] private static extern uint PrivateExtractIcons(string l, int n, int cx, int cy, ref IntPtr p, IntPtr id, uint ni, uint fl);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("kernel32.dll")] private static extern int GetPrivateProfileString(string s, string k, string d, System.Text.StringBuilder r, int z, string f);
}
