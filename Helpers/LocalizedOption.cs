namespace Launchbox.Helpers;

internal sealed record LocalizedOption(string Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
