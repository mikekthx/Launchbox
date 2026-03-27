using System.Collections.Generic;

namespace Launchbox.Helpers;

public static class Constants
{
    public const int WINDOW_WIDTH = 650;
    public const int WINDOW_HEIGHT = 700;
    public const int MIN_WINDOW_WIDTH = 300;
    public const int MIN_WINDOW_HEIGHT = 200;

    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;

    public const int MIN_VIRTUAL_KEY = 0x01;
    public const int MAX_VIRTUAL_KEY = 0xFE;

    public const int VK_S = 0x53;
    public const int HOTKEY_ID = 9000;

    // Icon constants — 256 covers up to ~457% DPI when displayed at 56 DIPs
    public const int ICON_SIZE = 256;
    public const int MIN_VALID_YEAR = 1900;
    public const long MAX_ICON_FILE_SIZE_BYTES = 5 * 1024 * 1024;

    public const string DWM_BLUR_GLASS_PROCESS_NAME = "DWMBlurGlass";
    public const string INTERNET_SHORTCUT_SECTION = "InternetShortcut";
    public const string ICON_FILE_KEY = "IconFile";
    public const string ICONS_DIR = ".icons";

    public static readonly IReadOnlyList<string> ALLOWED_EXTENSIONS = [".lnk", ".url"];
}
