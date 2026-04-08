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

    // Display dimensions (DIPs) for the UI grid
    public const int ITEM_WIDTH_SMALL = 80;
    public const int ITEM_WIDTH_MEDIUM = 110;
    public const int ITEM_WIDTH_LARGE = 140;

    public const int ITEM_HEIGHT_SMALL = 96;
    public const int ITEM_HEIGHT_MEDIUM = 130;
    public const int ITEM_HEIGHT_LARGE = 165;

    // Display sizes (DIPs) for the icons in the UI grid
    public const int ICON_SIZE_SMALL = 32;
    public const int ICON_SIZE_MEDIUM = 56;
    public const int ICON_SIZE_LARGE = 72;

    // Icon extraction constants — 256 covers up to ~457% DPI when displayed at 56 DIPs
    // Note: ICON_SIZE is the extraction resolution, whereas ICON_SIZE_SMALL/MEDIUM/LARGE are UI display sizes.
    public const int ICON_SIZE = 256;
    public const int MIN_VALID_YEAR = 1900;
    public const long MAX_ICON_FILE_SIZE_BYTES = 5 * 1024 * 1024;

    public const string DWM_BLUR_GLASS_PROCESS_NAME = "DWMBlurGlass";
    public const string INTERNET_SHORTCUT_SECTION = "InternetShortcut";
    public const string ICON_FILE_KEY = "IconFile";
    public const string ICONS_DIR = ".icons";

    public static readonly IReadOnlyList<string> ALLOWED_EXTENSIONS = [".lnk", ".url"];
}
