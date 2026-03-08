using Launchbox.Helpers;
using Xunit;

namespace Launchbox.Tests;

public class ConstantsTests
{
    [Fact]
    public void WindowDimensions_HaveExpectedValues()
    {
        Assert.Equal(650, Constants.WINDOW_WIDTH);
        Assert.Equal(700, Constants.WINDOW_HEIGHT);
    }

    [Fact]
    public void ModifierKeys_HaveExpectedValues()
    {
        Assert.Equal(0x0001, Constants.MOD_ALT);
        Assert.Equal(0x0002, Constants.MOD_CONTROL);
        Assert.Equal(0x0004, Constants.MOD_SHIFT);
        Assert.Equal(0x0008, Constants.MOD_WIN);
    }

    [Fact]
    public void HotkeyConstants_HaveExpectedValues()
    {
        Assert.Equal(0x53, Constants.VK_S);
        Assert.Equal(9000, Constants.HOTKEY_ID);
    }

    [Fact]
    public void IconConstants_HaveExpectedValues()
    {
        Assert.Equal(96, Constants.ICON_SIZE);
        Assert.Equal(1900, Constants.MIN_VALID_YEAR);
        Assert.Equal(5 * 1024 * 1024, Constants.MAX_ICON_FILE_SIZE_BYTES);
    }

    [Fact]
    public void StringConstants_HaveExpectedValues()
    {
        Assert.Equal("DWMBlurGlass", Constants.DWM_BLUR_GLASS_PROCESS_NAME);
        Assert.Equal("InternetShortcut", Constants.INTERNET_SHORTCUT_SECTION);
        Assert.Equal("IconFile", Constants.ICON_FILE_KEY);
        Assert.Equal(".icons", Constants.ICONS_DIR);
    }

    [Fact]
    public void AllowedExtensions_HaveExpectedValues()
    {
        Assert.Equal(new[] { ".lnk", ".url" }, Constants.ALLOWED_EXTENSIONS);
    }
}
