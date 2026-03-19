using Launchbox.Helpers;
using Xunit;

namespace Launchbox.Tests;

public class LocalizedOptionTests
{
    [Fact]
    public void ToString_ReturnsDisplayName()
    {
        var option = new LocalizedOption("Small", "Pequeño");

        Assert.Equal("Pequeño", option.ToString());
    }

    [Fact]
    public void Value_StoresStorageKey()
    {
        var option = new LocalizedOption("Alt", "Alternativa");

        Assert.Equal("Alt", option.Value);
        Assert.Equal("Alternativa", option.DisplayName);
    }

    [Fact]
    public void Equality_BasedOnValueAndDisplayName()
    {
        var a = new LocalizedOption("Small", "Small");
        var b = new LocalizedOption("Small", "Small");

        Assert.Equal(a, b);
    }
}
