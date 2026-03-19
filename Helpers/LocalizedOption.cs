namespace Launchbox.Helpers;

public sealed record LocalizedOption(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
