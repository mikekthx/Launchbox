using Launchbox.Helpers;
using Launchbox.Services;
using Xunit;

namespace Launchbox.Tests;

[Collection("Localization")]
public class LocalizationTests
{
    [Fact]
    public void GetString_ReturnsValueFromProvider()
    {
        var mock = new MockStringProvider(new()
        {
            { "TestKey", "TestValue" }
        });
        Localization.SetProvider(mock);

        var result = Localization.GetString("TestKey");

        Assert.Equal("TestValue", result);
    }

    [Fact]
    public void SetProvider_ReplacesActiveProvider()
    {
        var first = new MockStringProvider(new() { { "Key", "First" } });
        var second = new MockStringProvider(new() { { "Key", "Second" } });

        Localization.SetProvider(first);
        Assert.Equal("First", Localization.GetString("Key"));

        Localization.SetProvider(second);
        Assert.Equal("Second", Localization.GetString("Key"));
    }
}
